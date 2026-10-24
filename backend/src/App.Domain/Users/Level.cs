namespace App.Domain.Users;

// Level n (n >= 2) needs 100*n XP on top of the previous one, so reaching n takes 50*n*(n+1) - 100 XP in total.
public static class Level
{
    public static int XpToReach(int level) => 50 * level * (level + 1) - 100;

    public static int Of(int xp)
    {
        var level = 1;
        while (XpToReach(level + 1) <= xp) level++;
        return level;
    }

    public static int XpToNext(int xp) => XpToReach(Of(xp) + 1) - xp;
}
