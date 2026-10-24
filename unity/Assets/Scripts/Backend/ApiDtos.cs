using System;

namespace ProjectPenguin.Backend
{
    // api/openapi.yaml の本文を JsonUtility で読むための型。名前は契約の JSON の項目名に合わせる (snake_case)。
    // JsonUtility は知らない項目を読み飛ばし、無い項目は既定値のままにする。省略可の項目は、既定値 (空文字・0) を
    // 「無い」として扱う。画面とゲームが使う形への変換は PenguinApiClient と各 Http* の口が行う。
#pragma warning disable IDE1006 // 契約の項目名に合わせる

    [Serializable]
    internal sealed class HealthDto
    {
        public string status;
        public string version;
        public string schema_version;
    }

    [Serializable]
    internal sealed class ErrorBodyDto
    {
        public string code;
        public string message;
        public bool retryable;
    }

    [Serializable]
    internal sealed class ErrorResponseDto
    {
        public ErrorBodyDto error;
    }

    [Serializable]
    internal sealed class CheckDto
    {
        public string id;
        public string label;
        public bool passed;
        public string detail;
    }

    [Serializable]
    internal sealed class CommunityContributionDto
    {
        public string goal_id;
        public int added;
        public int current;
        public int target;
    }

    [Serializable]
    internal sealed class JudgeResultDto
    {
        public string schema_version;
        public string subject;
        public string comment;
        public double confidence;
        public string decision;
        public CheckDto[] checks;
        public string retake_hint;
        public CommunityContributionDto community;
    }

    [Serializable]
    internal sealed class MilestoneDto
    {
        public int at;
        public string reward_id;
        public bool reached;
        public string label;
    }

    [Serializable]
    internal sealed class CommunityGoalDto
    {
        public string schema_version;
        public bool enabled;
        public string goal_id;
        public string title;
        public string starts_at;
        public string ends_at;
        public int target;
        public int current;
        public MilestoneDto[] milestones;
        public string updated_at;
        public int poll_seconds;
    }

    [Serializable]
    internal sealed class RewardDto
    {
        public string kind;
        public string id;
        public double amount;
    }

    [Serializable]
    internal sealed class QuestDto
    {
        public string id;
        public string kind;
        public string title;
        public string description;
        public string starts_at;
        public string ends_at;
        public string proof;
        public string[] subjects;
        public RewardDto reward;
        public bool enabled;
    }

    [Serializable]
    internal sealed class QuestListDto
    {
        public string schema_version;
        public QuestDto[] quests;
        public int poll_seconds;
    }

    [Serializable]
    internal sealed class RedeemRequestDto
    {
        public string code;
    }

    [Serializable]
    internal sealed class RedeemResultDto
    {
        public string schema_version;
        public bool accepted;
        public RewardDto reward;
        public string message;
    }

#pragma warning restore IDE1006
}
