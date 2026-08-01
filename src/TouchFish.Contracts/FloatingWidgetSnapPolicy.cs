namespace TouchFish.Contracts;

public readonly record struct FloatingWidgetBounds(double Left, double Top, double Width, double Height)
{
    public double Right => Left + Width;
    public double Bottom => Top + Height;
}

public static class FloatingWidgetSnapPolicy
{
    public static (double Left, double Top) SnapToPeers(
        FloatingWidgetBounds current,
        IReadOnlyCollection<FloatingWidgetBounds> peers,
        double snapDistance = 16)
    {
        var left = current.Left;
        var top = current.Top;
        var bestHorizontalDistance = snapDistance + double.Epsilon;
        var bestVerticalDistance = snapDistance + double.Epsilon;

        foreach (var peer in peers)
        {
            if (RangesNear(current.Top, current.Bottom, peer.Top, peer.Bottom, snapDistance))
            {
                Evaluate(current.Left, peer.Left - current.Width, ref left, ref bestHorizontalDistance);
                Evaluate(current.Left, peer.Left, ref left, ref bestHorizontalDistance);
                Evaluate(current.Left, peer.Right - current.Width, ref left, ref bestHorizontalDistance);
                Evaluate(current.Left, peer.Right, ref left, ref bestHorizontalDistance);
            }

            if (RangesNear(current.Left, current.Right, peer.Left, peer.Right, snapDistance))
            {
                Evaluate(current.Top, peer.Top - current.Height, ref top, ref bestVerticalDistance);
                Evaluate(current.Top, peer.Top, ref top, ref bestVerticalDistance);
                Evaluate(current.Top, peer.Bottom - current.Height, ref top, ref bestVerticalDistance);
                Evaluate(current.Top, peer.Bottom, ref top, ref bestVerticalDistance);
            }
        }

        return (left, top);
    }

    private static void Evaluate(double current, double candidate, ref double result, ref double bestDistance)
    {
        var distance = Math.Abs(current - candidate);
        if (distance <= bestDistance)
        {
            result = candidate;
            bestDistance = distance;
        }
    }

    private static bool RangesNear(double firstStart, double firstEnd, double secondStart, double secondEnd, double distance) =>
        firstEnd + distance >= secondStart && secondEnd + distance >= firstStart;
}
