using System;
using System.IO;
using UnityEngine;

public static class MultiplayerPolishChecks
{
    public static void RunAll()
    {
        try
        {
            WeaponEconomyChecks.Run();
            WeaponExpansionChecks.Run();
            MobileControlLayoutChecks.Run();
            HudLayoutChecks.Run();
            DuelRoofAccessChecks.Run();
            DuelVisualChecks.RunAll();
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/MultiplayerPolishChecks.txt",
                "PASS: touch control persistence and input, combat HUD layouts, mutual rematches, duel gameplay and rendering.\n");
        }
        catch(Exception error)
        {
            Debug.LogException(error);
            throw;
        }
    }
}
