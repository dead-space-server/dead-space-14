using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Content.Server.Administration.Managers;
using Robust.Server.ServerStatus;
using Content.Server.Database;
using Robust.Shared.Toolshed.TypeParsers;

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
            await context.RespondJsonAsync("Erorr");
        var playersList = new JsonArray();
        var ranks = await _db.GetAllAdminAndRanksAsync();
        foreach (var keq in ranks.Item2)
        {
            foreach (var hue in keq.Flags)
            {
                var obj = new JsonObject
                {
                    ["Id"] = hue.Id,
                    ["Name"] = keq.Name,
                    ["Flag"] = hue.Flag
                };
                playersList.Add(obj);
            }
        }
        await context.RespondJsonAsync(playersList);
    }
}
