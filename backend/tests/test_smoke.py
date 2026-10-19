"""パッケージが import できることの確認。実装が入ったら差し替える。"""

import penguin_backend


def test_version_is_exposed() -> None:
    assert penguin_backend.__version__
