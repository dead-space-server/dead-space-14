#!/usr/bin/env python3

"""
Sends DS14 changelog updates to a Discord webhook after a successful Publish run.

The script compares the current changelog with the changelog from the previous
successful Publish workflow run, then posts all new entries in Discord-sized
messages.
"""

import os
import sys
import urllib.parse
from pathlib import Path
from typing import Any, Iterable, Optional
from datetime import datetime, timedelta, timezone

import requests
import yaml

import actions_changelog_github

DEBUG = os.environ.get("SS14_CHANGELOG_DEBUG", "").lower() in {"1", "true", "yes"}
DRY_RUN = os.environ.get("DRY_RUN", "").lower() in {"1", "true", "yes"}
DEBUG_CHANGELOG_FILE_OLD = Path("Resources/Changelog/Old.yml")
<<<<<<< ours
DEBUG_LAST_PUBLISH_DATE = "01.01.1984 12:00 (UTC+3)"
GITHUB_API_URL = os.environ.get("GITHUB_API_URL", "https://api.github.com")
||||||| base
GITHUB_API_URL = os.environ.get("GITHUB_API_URL", "https://api.github.com")
=======
DEBUG_DISCORD_DUMP_FILE = Path("Resources/Changelog/DiscordDebug.md")
>>>>>>> theirs

# https://discord.com/developers/docs/resources/webhook
DISCORD_SPLIT_LIMIT = 2000
DISCORD_WEBHOOK_URL = os.environ.get("DISCORD_WEBHOOK_URL")
<<<<<<< ours
DISCORD_USERNAME = "Капибарка МК-Изменения"
DISCORD_EMBED_COLOR = 3300375

CHANGELOG_FILE = "Resources/Changelog/ChangelogDS14.yml"
MOSCOW_TZ = timezone(timedelta(hours=3))

TYPE_ORDER = ("Add", "Remove", "Tweak", "Fix")
TYPE_LABELS = {
    "Add": "🆕 Добавлено:",
    "Remove": "❌ Удалено:",
    "Tweak": "⚒️ Изменено:",
    "Fix": "🐛 Исправлено:",
}
SECTION_TITLE_TO_TYPE = {
    "Добавлено": "Add",
    "Удалено": "Remove",
    "Изменено": "Tweak",
    "Исправлено": "Fix",
}
UNKNOWN_TYPE_LABEL = "❓ Прочее:"

EMBED_TITLE = "Все изменения от {date}"
MESSAGE_FOOTER = "Данные изменения были опубликованы на все основные сервера проекта!"
||||||| base

CHANGELOG_FILE = "Resources/Changelog/Changelog.yml"

TYPES_TO_EMOJI = {"Fix": "🐛", "Add": "🆕", "Remove": "❌", "Tweak": "⚒️"}
=======
TRUNCATION_SUFFIX = " [...]"

CHANGELOG_FILE = "Resources/Changelog/Changelog.yml"

TYPES_TO_EMOJI = {"Fix": "🐛", "Add": "🆕", "Remove": "❌", "Tweak": "⚒️"}
>>>>>>> theirs

EXPERIMENTAL_LABEL = "Intent: Experimental"
EXPERIMENTAL_EMOJI = "🧪"

ChangelogEntry = dict[str, Any]
GroupedChanges = dict[str, dict[str, list[str]]]


def main():
    if not DEBUG and not DRY_RUN and not DISCORD_WEBHOOK_URL:
        print("No discord webhook URL found, skipping discord send")
        return

    if DEBUG:
<<<<<<< ours
        last_changelog_stream = DEBUG_CHANGELOG_FILE_OLD.read_text(encoding="utf-8")
        last_publish_date = DEBUG_LAST_PUBLISH_DATE
||||||| base
        # to debug this script locally, you can use
        # a separate local file as the old changelog
        last_changelog_stream = DEBUG_CHANGELOG_FILE_OLD.read_text()
=======
        # to debug this script locally, you can use
        # a separate local file as the old changelog
        last_changelog_stream = DEBUG_CHANGELOG_FILE_OLD.read_text(encoding="utf-8-sig")
>>>>>>> theirs
    else:
        last_changelog = get_last_changelog()
        if last_changelog is None:
            print("No previous successful publish run found, skipping discord send")
            return

<<<<<<< ours
        last_changelog_stream, last_publish_date = last_changelog

    last_changelog_data = yaml.safe_load(last_changelog_stream) or {}
    with open(CHANGELOG_FILE, "r", encoding="utf-8") as f:
        cur_changelog = yaml.safe_load(f) or {}

    diff = list(diff_changelog(last_changelog_data, cur_changelog))
    if not diff:
        print("No changelog changes found since the last publish")
        return
||||||| base
    last_changelog = yaml.safe_load(last_changelog_stream)
    with open(CHANGELOG_FILE, "r") as f:
        cur_changelog = yaml.safe_load(f)
=======
    last_changelog = yaml.safe_load(last_changelog_stream)
    with open(CHANGELOG_FILE, "r", encoding="utf-8-sig") as f:
        cur_changelog = yaml.safe_load(f)
>>>>>>> theirs

<<<<<<< ours
    messages = changelog_entries_to_messages(diff, last_publish_date)
    send_messages(messages, last_publish_date)
||||||| base
    diff = diff_changelog(last_changelog, cur_changelog)
    message_lines = changelog_entries_to_message_lines(diff)
    send_message_lines(message_lines)
=======
    diff = diff_changelog(last_changelog, cur_changelog)
    message_lines = changelog_entries_to_message_lines(diff)
>>>>>>> theirs

    if DEBUG:
        dump_debug_markdown(message_lines)
        return

<<<<<<< ours
def get_most_recent_workflow(
    sess: requests.Session, github_repository: str, github_run: str
) -> Optional[Any]:
    workflow_run = get_current_run(sess, github_repository, github_run)
    past_runs = (
        run for run in get_past_runs(sess, workflow_run)
        if run["conclusion"] == "success"
        and run["id"] < workflow_run["id"]
        and run["created_at"] <= workflow_run["created_at"]
    )
    return max(past_runs, key=lambda run: (run["created_at"], run["id"]), default=None)
||||||| base
def get_most_recent_workflow(
    sess: requests.Session, github_repository: str, github_run: str
) -> Any:
    workflow_run = get_current_run(sess, github_repository, github_run)
    past_runs = get_past_runs(sess, workflow_run)
    for run in past_runs["workflow_runs"]:
        # First past successful run that isn't our current run.
        if run["id"] == workflow_run["id"]:
            continue

        return run
=======
    if DRY_RUN:
        log_message_lines_dry_run(message_lines)
        return

    send_message_lines(message_lines)
>>>>>>> theirs


def get_last_changelog() -> str:
    github_repository, github_run, github_token = (
        actions_changelog_github.get_required_github_env()
    )

<<<<<<< ours
def get_past_runs(sess: requests.Session, current_run: Any) -> Iterable[Any]:
    """
    Get successful workflow runs on the current branch across all result pages.
    """
    # Avoid the created search filter and do not assume the first result is the newest run.
    params = {"status": "success", "branch": current_run["head_branch"], "per_page": 100}
    url = f"{current_run['workflow_url']}/runs"
    while url:
        resp = sess.get(url, params=params)
        resp.raise_for_status()
        yield from resp.json()["workflow_runs"]
        url = resp.links.get("next", {}).get("url")
        params = None


def get_last_changelog() -> Optional[tuple[str, str]]:
    github_repository = os.environ["GITHUB_REPOSITORY"]
    github_run = os.environ["GITHUB_RUN_ID"]
    github_token = os.environ["GITHUB_TOKEN"]

    session = requests.Session()
    session.headers["Authorization"] = f"Bearer {github_token}"
    session.headers["Accept"] = "application/vnd.github+json"
    session.headers["X-GitHub-Api-Version"] = "2022-11-28"

    most_recent = get_most_recent_workflow(session, github_repository, github_run)
    if most_recent is None:
        return None

    head_commit = most_recent.get("head_commit") or {}
    last_sha = most_recent.get("head_sha") or head_commit["id"]
    last_publish_date = format_publish_date(most_recent["created_at"])
    print(f"Last successful publish job was {most_recent['id']}: {last_sha}")
    last_changelog_stream = get_last_changelog_by_sha(
        session, last_sha, github_repository
    )

    return last_changelog_stream, last_publish_date


def format_publish_date(created_at: str) -> str:
    published_at = datetime.fromisoformat(created_at.replace("Z", "+00:00"))
    return published_at.astimezone(MOSCOW_TZ).strftime("%d.%m.%Y %H:%M (UTC+3)")
||||||| base
def get_past_runs(sess: requests.Session, current_run: Any) -> Any:
    """
    Get all successful workflow runs before our current one.
    """
    params = {"status": "success", "created": f"<={current_run['created_at']}"}
    resp = sess.get(f"{current_run['workflow_url']}/runs", params=params)
    resp.raise_for_status()
    return resp.json()


def get_last_changelog() -> str:
    github_repository = os.environ["GITHUB_REPOSITORY"]
    github_run = os.environ["GITHUB_RUN_ID"]
    github_token = os.environ["GITHUB_TOKEN"]

    session = requests.Session()
    session.headers["Authorization"] = f"Bearer {github_token}"
    session.headers["Accept"] = "Accept: application/vnd.github+json"
    session.headers["X-GitHub-Api-Version"] = "2022-11-28"

    most_recent = get_most_recent_workflow(session, github_repository, github_run)
    last_sha = most_recent["head_commit"]["id"]
    print(f"Last successful publish job was {most_recent['id']}: {last_sha}")
    last_changelog_stream = get_last_changelog_by_sha(
        session, last_sha, github_repository
    )

    return last_changelog_stream
=======
    session = actions_changelog_github.make_github_session(github_token)

    # If a previous workflow step already computed the last successful publish's
    # SHA, reuse it instead of querying the GitHub Actions API for the same info.
    last_sha = os.environ.get("LAST_PUBLISH_SHA")
    if last_sha:
        print(f"Using last publish SHA from environment: {last_sha}")
    else:
        last_sha = actions_changelog_github.get_last_publish_sha(
            session, github_repository, github_run
        )

    return get_last_changelog_by_sha(session, last_sha, github_repository)
>>>>>>> theirs


def get_last_changelog_by_sha(
    sess: requests.Session, sha: str, github_repository: str
) -> str:
    """
    Use GitHub API to get the previous changelog YAML.

    Actions builds are fetched with a shallow clone, so local git history is not
    reliable here.
    """
    params = {
        "ref": sha,
    }
    headers = {"Accept": "application/vnd.github.raw"}

    resp = sess.get(
        f"{actions_changelog_github.GITHUB_API_URL}/repos/{github_repository}/contents/{CHANGELOG_FILE}",
        headers=headers,
        params=params,
    )
    resp.raise_for_status()
    return resp.text


def diff_changelog(
    old: dict[str, Any], cur: dict[str, Any]
) -> Iterable[ChangelogEntry]:
    """
    Find all new entries not present in the previous publish.
    """
    old_entry_ids = {entry.get("id") for entry in old.get("Entries", [])}
    for entry in cur.get("Entries", []):
        if entry.get("id") not in old_entry_ids:
            yield entry


def changelog_entries_to_messages(
    entries: Iterable[ChangelogEntry], last_publish_date: str
) -> list[str]:
    grouped_changes = group_changes_by_author(entries)
    messages: list[str] = []
    current_body = ""

    for author, sections in grouped_changes.items():
        author_block = render_author_block(author, sections)
        if not author_block:
            continue

        if len(author_block) > DISCORD_SPLIT_LIMIT:
            if current_body:
                messages.append(current_body)
                current_body = ""

            for split_block in split_author_block(author, sections, last_publish_date):
                messages.append(split_block)

            continue

        candidate_body = join_message_bodies(current_body, author_block)
        if current_body and len(candidate_body) > DISCORD_SPLIT_LIMIT:
            messages.append(current_body)
            current_body = author_block
        else:
            current_body = candidate_body

    if current_body:
        messages.append(current_body)

    for message in messages:
        if len(message) > DISCORD_SPLIT_LIMIT:
            raise RuntimeError("Generated Discord changelog embed description exceeds the split limit")

    return messages


def group_changes_by_author(entries: Iterable[ChangelogEntry]) -> GroupedChanges:
    grouped: GroupedChanges = {}

    for entry in entries:
        author = str(entry.get("author") or "unknown")
        if author not in grouped:
            grouped[author] = {type_key: [] for type_key in TYPE_ORDER}

        for change in entry.get("changes", []):
            type_key = str(change.get("type") or "")
            message = normalize_change_message(change.get("message", ""))
            sanitized = sanitize_change(type_key, message)
            if sanitized is None:
                continue

            type_key, message = sanitized
            if message in grouped[author].setdefault(type_key, []):
                continue

            grouped[author].setdefault(type_key, []).append(message)

    return grouped


def normalize_change_message(message: Any) -> str:
    return " ".join(str(message).split())


def sanitize_change(type_key: str, message: str) -> Optional[tuple[str, str]]:
    if not message:
        return None

    for section_title, section_type in SECTION_TITLE_TO_TYPE.items():
        prefix = f"{section_title}:"
        if message == prefix or message == section_title:
            return None

        if not message.startswith(prefix):
            continue

        message = message[len(prefix):].strip()
        if not message:
            return None

        return section_type, message

    return type_key, message


def render_author_block(author: str, sections: dict[str, list[str]]) -> str:
    lines = [f"**{author}**"]
    wrote_section = False

    for type_key in iter_section_keys(sections):
        messages = sections[type_key]
        if not messages:
            continue

        if wrote_section:
            lines.append("")

        lines.append(section_label(type_key))
        lines.extend(f"- {message}" for message in messages)
        wrote_section = True

    if not wrote_section:
        return ""

    return "\n".join(lines)


def iter_section_keys(sections: dict[str, list[str]]) -> Iterable[str]:
    for type_key in TYPE_ORDER:
        yield type_key

    for type_key in sections:
        if type_key not in TYPE_ORDER:
            yield type_key


def section_label(type_key: str) -> str:
    return TYPE_LABELS.get(type_key, UNKNOWN_TYPE_LABEL)


def split_author_block(
    author: str, sections: dict[str, list[str]], last_publish_date: str
) -> list[str]:
    split_blocks: list[str] = []
    current_lines: list[str] = []

    def flush_current():
        nonlocal current_lines

        if current_lines:
            split_blocks.append("\n".join(current_lines))

        current_lines = []

    def start_section(type_key: str):
        nonlocal current_lines

        current_lines = [f"**{author}**", section_label(type_key)]

    for type_key in iter_section_keys(sections):
        flush_current()

        for message in sections[type_key]:
            for segment in split_change_message(author, type_key, message, last_publish_date):
                line = f"- {segment}"

                if not current_lines:
                    start_section(type_key)

                candidate_lines = [*current_lines, line]
                if len("\n".join(candidate_lines)) <= DISCORD_SPLIT_LIMIT:
                    current_lines.append(line)
                    continue

                flush_current()
                start_section(type_key)
                candidate_lines = [*current_lines, line]
                if len("\n".join(candidate_lines)) > DISCORD_SPLIT_LIMIT:
                    raise RuntimeError("Unable to split a changelog entry below Discord's limit")

                current_lines.append(line)

    flush_current()
    return split_blocks


def split_change_message(
    author: str, type_key: str, message: str, last_publish_date: str
) -> Iterable[str]:
    probe_body = "\n".join([f"**{author}**", section_label(type_key), "- x"])
    segment_limit = DISCORD_SPLIT_LIMIT - len(probe_body) + 1
    if segment_limit <= 0:
        raise RuntimeError("Discord changelog message overhead exceeds the split limit")

    text = message.strip()
    while len(text) > segment_limit:
        split_at = text.rfind(" ", 0, segment_limit + 1)
        if split_at <= 0:
            split_at = segment_limit

        yield text[:split_at].rstrip()
        text = text[split_at:].lstrip()

    if text:
        yield text


def join_message_bodies(left: str, right: str) -> str:
    if not left:
        return right

    if not right:
        return left

    return f"{left}\n\n{right}"


def get_discord_embed(description: str, last_publish_date: str, include_footer: bool):
    embed = {
        "color": DISCORD_EMBED_COLOR,
        "title": EMBED_TITLE.format(date=last_publish_date),
        "description": description,
    }

    if include_footer:
        embed["footer"] = {
            "text": MESSAGE_FOOTER,
        }

    return embed


def get_discord_body(description: str, last_publish_date: str, include_footer: bool):
    return {
        "username": DISCORD_USERNAME,
        "embeds": [
            get_discord_embed(description, last_publish_date, include_footer),
        ],
        # Do not allow any mentions.
        "allowed_mentions": {"parse": []},
    }


def send_discord_webhook(description: str, last_publish_date: str, include_footer: bool):
    body = get_discord_body(description, last_publish_date, include_footer)
    retry_attempt = 0

    try:
        response = requests.post(DISCORD_WEBHOOK_URL, json=body, timeout=10)
        while response.status_code == 429:
            retry_attempt += 1
            if retry_attempt > 20:
                print("Too many retries on a single request despite following retry_after header... giving up")
                exit(1)

            retry_after = response.json().get("retry_after", 5)
            print(f"Rate limited, retrying after {retry_after} seconds")
            time.sleep(retry_after)
            response = requests.post(DISCORD_WEBHOOK_URL, json=body, timeout=10)

        response.raise_for_status()
    except requests.exceptions.RequestException as e:
        print(f"Failed to send message: {e}")
        exit(1)


<<<<<<< ours
def send_messages(messages: list[str], last_publish_date: str):
    for index, message in enumerate(messages, start=1):
        print(f"Sending changelog embed {index}/{len(messages)} to discord")
        send_discord_webhook(message, last_publish_date, index == len(messages))
||||||| base
def changelog_entries_to_message_lines(entries: Iterable[ChangelogEntry]) -> list[str]:
    """Process structured changelog entries into a list of lines making up a formatted message."""
    message_lines = []

    for contributor_name, group in itertools.groupby(entries, lambda x: x["author"]):
        message_lines.append("\n")
        message_lines.append(f"**{contributor_name}** updated:\n")

        for entry in group:
            url = entry.get("url")
            if url and not url.strip():
                url = None

            for change in entry["changes"]:
                emoji = TYPES_TO_EMOJI.get(change["type"], "❓")
                message = change["message"]

                # if a single line is longer than the limit, it needs to be truncated
                if len(message) > DISCORD_SPLIT_LIMIT:
                    message = message[: DISCORD_SPLIT_LIMIT - 100].rstrip() + " [...]"

                if url is not None:
                    pr_number = url.split("/")[-1]
                    line = f"{emoji} - {message} ([#{pr_number}]({url}))\n"
                else:
                    line = f"{emoji} - {message}\n"

                message_lines.append(line)

    return message_lines


def send_message_lines(message_lines: list[str]):
    """Join a list of message lines into chunks that are each below Discord's message length limit, and send them."""
    chunk_lines = []
    chunk_length = 0

    for line in message_lines:
        line_length = len(line)
        new_chunk_length = chunk_length + line_length

        if new_chunk_length > DISCORD_SPLIT_LIMIT:
            print("Split changelog and sending to discord")
            send_discord_webhook(chunk_lines)

            new_chunk_length = line_length
            chunk_lines.clear()

        chunk_lines.append(line)
        chunk_length = new_chunk_length

    if chunk_lines:
        print("Sending final changelog to discord")
        send_discord_webhook(chunk_lines)
=======
def truncate_to_limit(text: str, limit: int) -> str:
    if len(text) <= limit:
        return text

    if limit <= len(TRUNCATION_SUFFIX):
        return TRUNCATION_SUFFIX[:limit]

    return text[: limit - len(TRUNCATION_SUFFIX)].rstrip() + TRUNCATION_SUFFIX


def create_change_line(emoji: str, message: str, url: str | None) -> str:
    if url is None:
        prefix = f"{emoji} - "
        suffix = "\n"
    else:
        pr_number = urllib.parse.urlparse(url).path.rstrip("/").split("/")[-1]
        prefix = f"{emoji} - "
        suffix = f" ([#{pr_number}]({url}))\n"

    available_message_length = DISCORD_SPLIT_LIMIT - len(prefix) - len(suffix)
    if available_message_length < 1:
        raise ValueError(f"Rendered changelog line has no room for a message: {url}")

    message = truncate_to_limit(message, available_message_length)
    return f"{prefix}{message}{suffix}"


def changelog_entries_to_message_lines(entries: Iterable[ChangelogEntry]) -> list[str]:
    """Process structured changelog entries into a list of lines making up a formatted message."""
    message_lines = []

    for contributor_name, group in itertools.groupby(entries, lambda x: x["author"]):
        message_lines.append("\n")
        message_lines.append(f"**{contributor_name}** updated:\n")

        for entry in group:
            url = entry.get("url")
            if url and not url.strip():
                url = None

            for change in entry["changes"]:
                emoji = TYPES_TO_EMOJI.get(change["type"], "❓")
                message = change["message"]

                labels = entry.get("labels") or []
                if EXPERIMENTAL_LABEL in labels:
                    emoji = f"{emoji}{EXPERIMENTAL_EMOJI}"

                message_lines.append(create_change_line(emoji, message, url))

    return message_lines


def split_message_lines(message_lines: list[str]) -> list[list[str]]:
    """Join message lines into chunks that are each below Discord's message length limit."""
    chunks = []
    chunk_lines = []
    chunk_length = 0

    for line in message_lines:
        line_length = len(line)
        if line_length > DISCORD_SPLIT_LIMIT:
            raise ValueError(
                f"Changelog line is too long for Discord after truncation: {line_length}"
            )

        new_chunk_length = chunk_length + line_length

        if new_chunk_length > DISCORD_SPLIT_LIMIT:
            if chunk_lines:
                chunks.append(chunk_lines)

            new_chunk_length = line_length
            chunk_lines = []

        chunk_lines.append(line)
        chunk_length = new_chunk_length

    if chunk_lines:
        chunks.append(chunk_lines)

    return chunks


def dump_debug_markdown(message_lines: list[str]):
    chunks = split_message_lines(message_lines)

    with DEBUG_DISCORD_DUMP_FILE.open("w", encoding="utf-8", newline="\n") as f:
        f.write("# Discord Changelog Debug Dump\n\n")
        f.write(
            f"Generated from `{DEBUG_CHANGELOG_FILE_OLD}` to `{CHANGELOG_FILE}`.\n\n"
        )

        if not chunks:
            f.write("_No changelog entries to send._\n")
            return

        for i, chunk_lines in enumerate(chunks, start=1):
            content = "".join(chunk_lines)
            f.write(
                f"<!-- Discord message break: chunk {i}/{len(chunks)}, {len(content)}/{DISCORD_SPLIT_LIMIT} characters -->\n\n"
            )
            f.write(f"## Discord Message {i}\n\n")
            f.write(content.lstrip("\n"))
            f.write("\n")

    print(f"Wrote Discord changelog debug dump to {DEBUG_DISCORD_DUMP_FILE}")


def send_message_lines(message_lines: list[str]):
    """Join a list of message lines into chunks that are each below Discord's message length limit, and send them."""
    chunks = split_message_lines(message_lines)

    for chunk_lines in chunks[:-1]:
        print("Split changelog and sending to discord")
        send_discord_webhook(chunk_lines)
>>>>>>> theirs

    if chunks:
        print("Sending final changelog to discord")
        send_discord_webhook(chunks[-1])


def log_message_lines_dry_run(message_lines: list[str]):
    """Log the Discord messages that would be sent, without sending them."""
    chunks = split_message_lines(message_lines)

    if not chunks:
        print("[DRY RUN] No changelog entries to publish.")
        return

    print(f"[DRY RUN] Would send {len(chunks)} Discord message(s). Dumping contents:")
    for i, chunk_lines in enumerate(chunks, start=1):
        print(f"[DRY RUN] --- Discord message {i}/{len(chunks)} ---")
        for line in chunk_lines:
            print(line, end="")
        print()


if __name__ == "__main__":
    try:
        main()
    except Exception as e:
        print(f"Failed to publish changelog to Discord: {e}", file=sys.stderr)
        exit(1)
