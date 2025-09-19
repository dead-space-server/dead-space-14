using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Content.Server.Administration.Managers;
using Robust.Server.ServerStatus;
using Content.Server.Database;
using Robust.Shared.Player;
using Robust.Shared.Toolshed.TypeParsers;
using System.Linq;

namespace Content.Server.Administration;

public sealed partial class ServerApi
{
    [Dependency] private readonly IServerDbManager _db = default!;
    /// <summary>
    /// Get players and active admins list
    /// </summary>
    private async Task GetPlayers(IStatusHandlerContext context)
    {
        var playersList = new JsonArray();
        foreach (var player in _playerManager.Sessions)
        {
            playersList.Add(player.Name);
        }

        var adminMgr = await RunOnMainThread(IoCManager.Resolve<IAdminManager>);
        var adminsDict = new JsonObject();

        foreach (var admin in adminMgr.AllAdmins)
        {
            var adminData = adminMgr.GetAdminData(admin, true)!;
            adminsDict[admin.Name] = new JsonObject
            {
                ["isActive"] = adminData.Active,
                ["isStealth"] = adminData.Stealth,
                ["title"] = adminData.Title,
                ["flags"] = JsonSerializer.SerializeToNode(adminData.Flags.ToString().Split(", ")),
            };
        }

        var jObject = new JsonObject
        {
            ["players"] = playersList,
            ["admins"] = adminsDict
        };

        await context.RespondJsonAsync(jObject);
    }
    private async Task SetAdminPermissions(IStatusHandlerContext context)
    {
        var permissionList = await ReadJson<PermissionActionBody>(context);
        if (permissionList == null)
        {
            await context.RespondJsonAsync("Erorr");
            return;
        }
        var adminMgr = await RunOnMainThread(IoCManager.Resolve<IAdminManager>);
        var playerMgr = await RunOnMainThread(IoCManager.Resolve<ISharedPlayerManager>);
        var ply = await _db.GetPlayerRecordByUserName(permissionList.Ckey);
        var playersList = new JsonArray();
        var ranks = await _db.GetAllAdminAndRanksAsync();
        Admin previoslyAdmin = new Admin();
        if (ply == null)
        {
            await context.RespondJsonAsync("Player not found");
            return;
        }

        bool isAdmin = false;
        foreach (var adminname in ranks.admins)
        {
            if (string.IsNullOrEmpty(adminname.lastUserName))
            {
                return;
            }
            if (ply.LastSeenUserName == adminname.lastUserName.ToString())
            {
                isAdmin = true;
            }
            var obj = new JsonObject
            {
                ["Name"] = adminname.lastUserName,
                ["IsAdmin"] = isAdmin,
                ["Flags"] = adminname.Item1.Flags.First().Flag
            };
            playersList.Add(obj);
            if (isAdmin)
            {
                previoslyAdmin = adminname.Item1;
                break;
            }
        }
        if (!isAdmin)
        {
            foreach (var admin in ranks.Item2)
            {
                if (admin.Name == null)
                {
                    return;
                }
                if (admin.Name == permissionList.Permissions)
                {
                    Admin adminTOGive = new Admin
                    {
                        //Flags = AdminFlagsHelper.NamesToFlags(flags),
                        AdminRankId = admin.Id,
                        AdminRank = admin,
                        UserId = ply.UserId,
                        Title = admin.Name,
                        Suspended = false,
                    };
                    await _db.AddAdminAsync(adminTOGive);
                    if (permissionList.OnServer)
                    {
                        adminMgr.ReloadAdmin(playerMgr.GetSessionById(ply.UserId));
                    }
                    break;
                }
            }
        }
        else
        {
            foreach (var admin in ranks.Item2)
            {
                if (admin.Name == null || previoslyAdmin == null)
                {
                    return;
                }
                if (admin.Name == permissionList.Permissions)
                {
                    List<AdminFlag> flags = new List<AdminFlag>();
                    foreach (var flag in admin.Flags)
                    {
                        flags.Add(new AdminFlag
                        {
                            Flag = flag.Flag,
                            Negative = false,
                        });
                    }
                    foreach (var flag in previoslyAdmin.Flags)
                    {
                        flags.Add(flag);
                    }
                    Admin adminTOGive = new Admin
                    {
                        Flags = flags,
                        AdminRankId = previoslyAdmin.AdminRankId,
                        AdminRank = previoslyAdmin.AdminRank,
                        UserId = previoslyAdmin.UserId,
                        Title = previoslyAdmin.Title,
                        Suspended = previoslyAdmin.Suspended,
                    };
                    await _db.AddAdminAsync(adminTOGive);
                    if (permissionList.OnServer)
                    {
                        adminMgr.ReloadAdmin(playerMgr.GetSessionById(ply.UserId));
                    }
                    break;
                }
            }
        }
        await context.RespondJsonAsync(playersList);
    }
}
