"""Compile and exercise the shared native OCR duration parser using the macOS SDK."""

import json
import subprocess
import tempfile
from pathlib import Path


def main() -> None:
    root = Path(__file__).resolve().parents[1]
    include = root / "unity/Assets/Plugins/iOS"
    cases = [
        ("45分", 45),
        ("0分", 0),
        ("1時間", 60),
        ("1時間30分", 90),
        ("2 時間\n15 分", 135),
        ("４５分", 45),
        ("24時間", 1440),
        ("24時間1分", -1),
        ("25時間", -1),
        ("1時間75分", -1),
        ("45m", 45),
        ("1h 30m", 90),
        ("2 hr 5 min", 125),
        ("2 hours 15 minutes", 135),
        ("30 MIN", 30),
        ("145時間", -1),
        ("1441分", -1),
        ("時間が表示されていません", -1),
        ("昨日\nスクリーンタイム\n45分\nアプリA 20分", 45),
    ]
    # The harness contains synthetic strings only. Nothing from the user's photos is read.
    checks = "\n".join(
        f"if (PenguinFactoryScreenMinutes(@{json.dumps(text, ensure_ascii=False)}) "
        f"!= {expected}) return {index + 1};"
        for index, (text, expected) in enumerate(cases)
    )
    source = (
        '#import "PenguinFactoryScreenMinutes.h"\n'
        f"int main(void) {{ @autoreleasepool {{\n{checks}\n}} return 0; }}\n"
    )
    with tempfile.TemporaryDirectory(prefix="factory-record-parser-") as directory:
        path = Path(directory)
        (path / "check.m").write_text(source)
        subprocess.run(  # noqa: S603 - fixed compiler and repository-owned synthetic harness
            [
                "/usr/bin/xcrun",
                "--sdk",
                "macosx",
                "clang",
                "-fobjc-arc",
                "-Wall",
                "-Werror",
                "-framework",
                "Foundation",
                "-I",
                str(include),
                str(path / "check.m"),
                "-o",
                str(path / "check"),
            ],
            check=True,
        )
        subprocess.run([str(path / "check")], check=True)  # noqa: S603 - compiled synthetic harness
    print(  # noqa: T201 - test summary only, never user image or OCR content
        f"Native duration parser: {len(cases)} synthetic cases passed "
        "(macOS Foundation; iOS build not verified)"
    )


if __name__ == "__main__":
    main()
