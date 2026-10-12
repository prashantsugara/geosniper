using System;
using System.IO;
using UnityEngine;

public static class PresentationRegressionChecks
{
    public static void RunAll()
    {
        try
        {
            AdFlowChecks.Run();
            DaylightRoofChecks.Run();
            DuelVisualChecks.RunAll();
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/PresentationRegressionChecks.txt", "PASS: Native ad lifecycle/layout, day/night roofs, duel gameplay and rendering.\n");
        }
        catch(Exception error)
        {
            Debug.LogException(error);
            throw;
        }
    }
}
