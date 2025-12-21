using System;

namespace GatherBuddy.Enums;

public enum GatheringType : byte
{
    Mining       = 0,
    Quarrying    = 1,
    Logging      = 2,
    Harvesting   = 3,
    Spearfishing = 4,
    Botanist     = 5,
    Miner        = 6,
    Fisher       = 7,
    Multiple     = 8,
    Unknown      = byte.MaxValue,
};

public static class GatheringTypeExtension
{
    public static GatheringType ToGroup(this GatheringType type)
    {
        return type switch
        {
            GatheringType.Mining       => GatheringType.Miner,
            GatheringType.Quarrying    => GatheringType.Miner,
            GatheringType.Miner        => GatheringType.Miner,
            GatheringType.Logging      => GatheringType.Botanist,
            GatheringType.Harvesting   => GatheringType.Botanist,
            GatheringType.Botanist     => GatheringType.Botanist,
            GatheringType.Spearfishing => GatheringType.Fisher,
            _                          => type,
        };
    }

    public static GatheringType Add(this GatheringType type, GatheringType other)
    {
        (type, other) = type < other ? (type, other) : (other, type);
        return type switch
        {
            GatheringType.Mining => other switch
            {
                GatheringType.Mining     => GatheringType.Mining,
                GatheringType.Quarrying  => GatheringType.Miner,
                GatheringType.Logging    => GatheringType.Multiple,
                GatheringType.Harvesting => GatheringType.Multiple,
                GatheringType.Botanist   => GatheringType.Multiple,
                GatheringType.Miner      => GatheringType.Miner,
                GatheringType.Multiple   => GatheringType.Multiple,
                GatheringType.Unknown    => GatheringType.Mining,
                _                        => throw new ArgumentOutOfRangeException(nameof(other), other, null),
            },
            GatheringType.Quarrying => other switch
            {
                GatheringType.Quarrying  => GatheringType.Quarrying,
                GatheringType.Logging    => GatheringType.Multiple,
                GatheringType.Harvesting => GatheringType.Multiple,
                GatheringType.Botanist   => GatheringType.Multiple,
                GatheringType.Miner      => GatheringType.Miner,
                GatheringType.Multiple   => GatheringType.Multiple,
                GatheringType.Unknown    => GatheringType.Quarrying,
                _                        => throw new ArgumentOutOfRangeException(nameof(other), other, null),
            },
            GatheringType.Logging => other switch
            {
                GatheringType.Logging    => GatheringType.Logging,
                GatheringType.Harvesting => GatheringType.Botanist,
                GatheringType.Botanist   => GatheringType.Botanist,
                GatheringType.Miner      => GatheringType.Multiple,
                GatheringType.Multiple   => GatheringType.Multiple,
                GatheringType.Unknown    => GatheringType.Logging,
                _                        => throw new ArgumentOutOfRangeException(nameof(other), other, null),
            },
            GatheringType.Harvesting => other switch
            {
                GatheringType.Harvesting => GatheringType.Harvesting,
                GatheringType.Botanist   => GatheringType.Botanist,
                GatheringType.Miner      => GatheringType.Multiple,
                GatheringType.Multiple   => GatheringType.Multiple,
                GatheringType.Unknown    => GatheringType.Harvesting,
                _                        => throw new ArgumentOutOfRangeException(nameof(other), other, null),
            },
            GatheringType.Botanist => other switch
            {
                GatheringType.Botanist => GatheringType.Botanist,
                GatheringType.Miner    => GatheringType.Multiple,
                GatheringType.Multiple => GatheringType.Multiple,
                GatheringType.Unknown  => GatheringType.Botanist,
                _                      => throw new ArgumentOutOfRangeException(nameof(other), other, null),
            },
            GatheringType.Miner => other switch
            {
                GatheringType.Miner    => GatheringType.Multiple,
                GatheringType.Multiple => GatheringType.Multiple,
                GatheringType.Unknown  => GatheringType.Miner,
                _                      => throw new ArgumentOutOfRangeException(nameof(other), other, null),
            },
            GatheringType.Multiple => GatheringType.Multiple,
            GatheringType.Unknown  => other,
            _                      => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };
    }

    public static string ToName(this GatheringType type)
    {
        return type switch
        {
            GatheringType.Mining       => "挖掘",
            GatheringType.Quarrying    => "碎石",
            GatheringType.Logging      => "伐木",
            GatheringType.Harvesting   => "割草",
            GatheringType.Spearfishing => "刺鱼",
            GatheringType.Botanist     => "园艺工",
            GatheringType.Miner        => "采矿工",
            GatheringType.Fisher       => "捕鱼人",
            GatheringType.Multiple     => "多种",
            GatheringType.Unknown      => "未知",
            _                          => type.ToString(),
        };
    }
}
