﻿using Godot;

internal static class Util
{
    public static Vector2 Constrain(this Vector2 v, float xMin, float xMax, float yMin, float yMax)
    {
        float x = v.x < xMin ? xMin : v.x;
        x = x > xMax ? xMax : x;

        float y = v.y < yMin ? yMin : v.y;
        y = y > yMax ? yMax : y;

        return new Vector2(x, y);
    }
}
