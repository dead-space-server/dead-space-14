#!/usr/bin/env python3

import argparse
import requests
import os
import subprocess
import time
from typing import Iterable

<<<<<<< ours
PUBLISH_TOKEN = os.environ["PUBLISH_TOKEN"]
VERSION = os.environ.get("PUBLISH_VERSION") or os.environ["GITHUB_SHA"]
||||||| base
PUBLISH_TOKEN = os.environ["PUBLISH_TOKEN"]
VERSION = os.environ["GITHUB_SHA"]
=======
DRY_RUN = os.environ.get("DRY_RUN", "").lower() in ("1", "true", "yes")
VERSION = os.environ["GITHUB_SHA"]
>>>>>>> theirs

RELEASE_DIR = "release"
PROGRESS_INTERVAL_SECONDS = 60

#
# CONFIGURATION PARAMETERS
# Forks should change these to publish to their own infrastructure.
#
ROBUST_CDN_URL = "https://cdn.deadspace14.net/"
FORK_ID = "dsfobos"


class ProgressFileReader:
    def __init__(self, file, file_name: str, total_bytes: int):
        self._file = file
        self._file_name = file_name
        self._total_bytes = total_bytes
        self._bytes_read = 0
        self._last_log_at = time.monotonic()

    @property
    def bytes_read(self) -> int:
        return self._bytes_read

    def __len__(self) -> int:
        return self._total_bytes

    def tell(self) -> int:
        return self._file.tell()

    def read(self, size=-1):
        chunk = self._file.read(size)
        if not chunk:
            return chunk

        self._bytes_read += len(chunk)
        now = time.monotonic()
        if now - self._last_log_at >= PROGRESS_INTERVAL_SECONDS:
            self._last_log_at = now
            sent_mb = self._bytes_read / 1024 / 1024
            total_mb = self._total_bytes / 1024 / 1024
            percent = self._bytes_read / self._total_bytes * 100 if self._total_bytes else 100
            print(f"    Uploading {self._file_name}: {sent_mb:.1f}/{total_mb:.1f} MB ({percent:.1f}%)")

        return chunk


def get_publish_token() -> str:
    try:
        return os.environ["PUBLISH_TOKEN"]
    except KeyError:
        raise RuntimeError("PUBLISH_TOKEN is not set")


def simulate_publish(fork_id: str, version: str) -> None:
    print(f"[DRY RUN] Would publish version {version} to Robust.CDN fork '{fork_id}'")
    print(f"[DRY RUN]   engine version: {get_engine_version()}")
    files = list(get_files_to_publish())
    print(f"[DRY RUN]   files to upload ({len(files)}):")
    for file in files:
        print(f"[DRY RUN]     - {file}")
    print("[DRY RUN] Skipping all network calls to Robust.CDN.")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--fork-id", default=FORK_ID)

    args = parser.parse_args()
    fork_id = args.fork_id


    if DRY_RUN:
        simulate_publish(fork_id, VERSION)
        return

    session = requests.Session()
    session.headers = {
        "Authorization": f"Bearer {get_publish_token()}",
    }

    engine_version = get_engine_version()
    print(f"Version: {VERSION}")
    print(f"Engine version: {engine_version}")
    print(f"Fork: {fork_id}")
    print(f"CDN: {ROBUST_CDN_URL}")

    def abort_publish():
        try:
            print(f"Aborting publish id {publish_id}...")
            session.post(
                f"{ROBUST_CDN_URL}fork/{fork_id}/publish/abort",
                json={"version": VERSION},
                headers={"Content-Type": "application/json", "Robust-Cdn-Publish-Id": publish_id},
                timeout=30)
        except Exception as e:
            print(f"Abort publish failed: {e}")

    data = {
        "version": VERSION,
        "engineVersion": engine_version,
    }
    headers = {
        "Content-Type": "application/json"
    }

    print(f"Starting publish...")
    resp = session.post(f"{ROBUST_CDN_URL}fork/{fork_id}/publish/start", json=data, headers=headers)
    if not resp.ok:
        print(f"Publish start FAILED: {resp.status_code} {resp.reason}")
        print(f"Response: {resp.text}")
        resp.raise_for_status()
    publish_id = resp.headers.get("Robust-Cdn-Publish-Id")
    if not publish_id:
        raise RuntimeError("CDN did not return Robust-Cdn-Publish-Id")
    print("Publish started OK, uploading files...")

    files = list(get_files_to_publish())
    print(f"Files to upload: {len(files)}")
    if not files:
        abort_publish()
        raise RuntimeError("No files found to publish")

    for file in files:
        try:
            resp = upload_file(session, fork_id, publish_id, file)
        except Exception:
            abort_publish()
            raise

        if not resp.ok:
            print(f"  Upload FAILED: {resp.status_code} {resp.reason}")
            print(f"  Response: {resp.text}")
            abort_publish()
            resp.raise_for_status()

    print("All files uploaded, finishing publish...")

    data = {
        "version": VERSION
    }
    headers = {
        "Content-Type": "application/json",
        "Robust-Cdn-Publish-Id": publish_id
    }
    try:
        resp = session.post(f"{ROBUST_CDN_URL}fork/{fork_id}/publish/finish", json=data, headers=headers)
    except Exception:
        abort_publish()
        raise

    if not resp.ok:
        print(f"Publish finish FAILED: {resp.status_code} {resp.reason}")
        print(f"Response: {resp.text}")
        abort_publish()
        resp.raise_for_status()

    print("SUCCESS!")


def upload_file(session: requests.Session, fork_id: str, publish_id: str, file: str) -> requests.Response:
    size_bytes = os.path.getsize(file)
    file_name = os.path.basename(file)
    size_mb = size_bytes / 1024 / 1024
    print(f"  Uploading {file_name} ({size_mb:.1f} MB)")

    with open(file, "rb") as f:
        body = ProgressFileReader(f, file_name, size_bytes)
        headers = {
            "Content-Type": "application/octet-stream",
            "Robust-Cdn-Publish-File": file_name,
            "Robust-Cdn-Publish-Version": VERSION,
            "Robust-Cdn-Publish-Id": publish_id
        }
        try:
            resp = session.post(f"{ROBUST_CDN_URL}fork/{fork_id}/publish/file", data=body, headers=headers)
        except requests.RequestException:
            sent_mb = body.bytes_read / 1024 / 1024
            print(f"  Upload exception for {file_name}; sent {sent_mb:.1f}/{size_mb:.1f} MB")
            raise

    print(f"  Upload finished {file_name}: {resp.status_code} {resp.reason}")
    return resp


def get_files_to_publish() -> Iterable[str]:
    for file in os.listdir(RELEASE_DIR):
        yield os.path.join(RELEASE_DIR, file)


def get_engine_version() -> str:
    import xml.etree.ElementTree as ET
    tree = ET.parse(os.path.join("RobustToolbox", "MSBuild", "Robust.Engine.Version.props"))
    version = tree.getroot().find(".//Version").text.strip()
    return version


if __name__ == '__main__':
    main()
