namespace TouchFish.Contracts;

public static class CursorWindowBoundsPolicy
{
    public static bool Contains(
        int cursorX,
        int cursorY,
        int left,
        int top,
        int right,
        int bottom) =>
        right > left &&
        bottom > top &&
        cursorX >= left &&
        cursorX < right &&
        cursorY >= top &&
        cursorY < bottom;
}
