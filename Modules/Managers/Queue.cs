﻿using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace RetakesPlugin.Modules.Managers;

public class Queue
{
    private readonly int _maxRetakesPlayers;
    private readonly float _terroristRatio;

    public List<CCSPlayerController> QueuePlayers = new();
    public List<CCSPlayerController> ActivePlayers = new();

    public List<CCSPlayerController> RoundTerrorists = new();
    public List<CCSPlayerController> RoundCounterTerrorists = new();

    public Queue(int? retakesMaxPlayers, float? retakesTerroristRatio)
    {
        _maxRetakesPlayers = retakesMaxPlayers ?? 9;
        _terroristRatio = retakesTerroristRatio ?? 0.45f;
    }

    public int GetTargetNumTerrorists()
    {
        var ratio = _terroristRatio * ActivePlayers.Count;
        var numTerrorists = (int)Math.Round(ratio);

        // Ensure at least one terrorist if the calculated number is zero
        return numTerrorists > 0 ? numTerrorists : 1;
    }
    
    public int GetTargetNumCounterTerrorists()
    {
        var targetPlayers = ActivePlayers.Count - GetTargetNumTerrorists();
        return targetPlayers > 0 ? targetPlayers : 1;
    }

    public void PlayerTriedToJoinTeam(CCSPlayerController player, CsTeam fromTeam, CsTeam toTeam, bool isWarmup)
    {
        Console.WriteLine($"{RetakesPlugin.LogPrefix}[{player.PlayerName}] PlayerTriedToJoinTeam called.");
        
        if (fromTeam == CsTeam.None && toTeam == CsTeam.Spectator)
        {
            // This is called when a player first joins.
            Console.WriteLine($"{RetakesPlugin.LogPrefix}[{player.PlayerName}] None -> Spectator.");
            return;
        }
        
        // TODO: Check RoundPlayer variables.
        
        var switchToSpectator = toTeam != CsTeam.Spectator;
        
        Console.WriteLine($"{RetakesPlugin.LogPrefix}[{player.PlayerName}] Checking ActivePlayers.");
        if (ActivePlayers.Contains(player))
        {
            Console.WriteLine($"{RetakesPlugin.LogPrefix}[{player.PlayerName}] Player is an active player.");
            
            if (toTeam == CsTeam.Spectator)
            {
                Console.WriteLine($"{RetakesPlugin.LogPrefix}[{player.PlayerName}] Switching to spectator.");
                ActivePlayers.Remove(player);
                return;
            }
            
            Console.WriteLine($"{RetakesPlugin.LogPrefix}[{player.PlayerName}] Do nothing.");
            return;
        }

        Console.WriteLine($"{RetakesPlugin.LogPrefix}[{player.PlayerName}] Checking QueuePlayers.");
        if (!QueuePlayers.Contains(player))
        {
            if (isWarmup && ActivePlayers.Count < _maxRetakesPlayers)
            {
                Console.WriteLine($"{RetakesPlugin.LogPrefix}[{player.PlayerName}] Not found, adding to ActivePlayers (because in warmup).");
                ActivePlayers.Add(player);
            }
            else
            {
                Console.WriteLine($"{RetakesPlugin.LogPrefix}[{player.PlayerName}] Not found, adding to QueuePlayers.");
                QueuePlayers.Add(player);
            }
        }
        else
        {
            Console.WriteLine($"{RetakesPlugin.LogPrefix}[{player.PlayerName}] Found in QueuePlayers, do nothing.");
        }

        Console.WriteLine($"{RetakesPlugin.LogPrefix}[{player.PlayerName}] Should switch to spectator? {(switchToSpectator ? "yes" : "no")}");
        if (switchToSpectator)
        {
            Console.WriteLine($"{RetakesPlugin.LogPrefix}[{player.PlayerName}] Changing to spectator.");
            player.ChangeTeam(CsTeam.Spectator);
        }
    }

    private void RemoveDisconnectedPlayers()
    {
        var disconnectedActivePlayers = ActivePlayers.Where(player => !Helpers.IsValidPlayer(player) || !Helpers.IsPlayerConnected(player)).ToList();

        if (disconnectedActivePlayers.Count > 0)
        {
            Console.WriteLine($"{RetakesPlugin.LogPrefix}Removing {disconnectedActivePlayers.Count} disconnected players from ActivePlayers.");
            ActivePlayers.RemoveAll(player => disconnectedActivePlayers.Contains(player));
        }
        
        var disconnectedQueuePlayers = QueuePlayers.Where(player => !Helpers.IsValidPlayer(player) || !Helpers.IsPlayerConnected(player)).ToList();
        
        if (disconnectedQueuePlayers.Count > 0)
        {
            Console.WriteLine($"{RetakesPlugin.LogPrefix}Removing {disconnectedQueuePlayers.Count} disconnected players from QueuePlayers.");
            QueuePlayers.RemoveAll(player => disconnectedQueuePlayers.Contains(player));
        }
    }

    private void AddConnectedPlayers()
    {
        var connectedPlayers = Utilities.GetPlayers().Where(player => Helpers.IsValidPlayer(player) && Helpers.IsPlayerConnected(player)).ToList();

        foreach (var connectedPlayer in connectedPlayers)
        {
            if (!ActivePlayers.Contains(connectedPlayer) && !QueuePlayers.Contains(connectedPlayer))
            {
                Console.WriteLine($"{RetakesPlugin.LogPrefix}Adding {connectedPlayer.PlayerName} to QueuePlayers.");
                QueuePlayers.Add(connectedPlayer);
            }
        }
    }
    
    public void Update()
    {
        RemoveDisconnectedPlayers();
        AddConnectedPlayers();
        
        var playersToAdd = _maxRetakesPlayers - ActivePlayers.Count;

        if (playersToAdd > 0 && QueuePlayers.Count > 0)
        {
            // Take players from QueuePlayers and add them to ActivePlayers
            var playersToAddList = QueuePlayers.Take(playersToAdd).ToList();
            
            QueuePlayers.RemoveAll(player => playersToAddList.Contains(player));
            ActivePlayers.AddRange(playersToAddList);
            
            // loop players to add, and set their team to CT
            foreach (var player in playersToAddList)
            {
                if (player.TeamNum == (int)CsTeam.Spectator)
                {
                    player.SwitchTeam(CsTeam.CounterTerrorist);
                }
            }
        }
    }

    public void PlayerDisconnected(CCSPlayerController player)
    {
        ActivePlayers.Remove(player);
        QueuePlayers.Remove(player);
    }
    
    public void DebugQueues(bool isBefore)
    {
        if (!ActivePlayers.Any())
        {
            Console.WriteLine($"{RetakesPlugin.LogPrefix}ActivePlayers ({(isBefore ? "BEFORE" : "AFTER")}): No active players.");
        }
        else
        {
            Console.WriteLine($"{RetakesPlugin.LogPrefix}ActivePlayers ({(isBefore ? "BEFORE" : "AFTER")}): {string.Join(", ", ActivePlayers.Where(Helpers.IsValidPlayer).Select(player => player.PlayerName))}");
        }

        if (!QueuePlayers.Any())
        {
            Console.WriteLine($"{RetakesPlugin.LogPrefix}QueuePlayers ({(isBefore ? "BEFORE" : "AFTER")}): No players in the queue.");
        }
        else
        {
            Console.WriteLine($"{RetakesPlugin.LogPrefix}QueuePlayers ({(isBefore ? "BEFORE" : "AFTER")}): {string.Join(", ", QueuePlayers.Where(Helpers.IsValidPlayer).Select(player => player.PlayerName))}");
        }
    }
    
    public void SetRoundTeams()
    {
        RoundTerrorists.Clear();
        RoundTerrorists = Utilities.GetPlayers().Where(player => Helpers.IsValidPlayer(player) && player.TeamNum == (int)CsTeam.Terrorist).ToList();
        
        RoundCounterTerrorists.Clear();
        RoundCounterTerrorists = Utilities.GetPlayers().Where(player => Helpers.IsValidPlayer(player) && player.TeamNum == (int)CsTeam.CounterTerrorist).ToList();
    }
}