﻿using System;
using System.Numerics;

namespace ProjBobcat.Class.Model.Version.Item;

public class BigIntegerItem : IItem
{
    readonly BigInteger _value;

    public BigIntegerItem(string bigIntStr)
    {
        _value = BigInteger.TryParse(bigIntStr, out var outBigInt) ? outBigInt : BigInteger.Zero;
    }

    public int CompareTo(object obj)
    {
        if (obj is not IItem item) return BigInteger.Zero == _value ? 0 : 1; // 1.0 == 1, 1.1 > 1

        return item switch
        {
            IntItem or LongItem => 1,
            BigIntegerItem biItem => _value.CompareTo(biItem._value),
            StringItem => 1, // 1.1 > 1-sp
            ListItem => 1, // 1.1 > 1-1
            _ => throw new ArgumentOutOfRangeException($"invalid item: {item.GetType()}")
        };
    }

    public bool IsNull()
    {
        return _value == BigInteger.Zero;
    }

    public override bool Equals(object obj)
    {
        if (this == obj) return true;
        if (obj is not BigIntegerItem that) return false;

        return _value.Equals(that._value);
    }

    public override int GetHashCode()
    {
        return _value.GetHashCode();
    }

    public override string ToString()
    {
        return _value.ToString();
    }
}