using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Singleton
    {
        get => _singleton;
        set
        {
            if (value == null)
                _singleton = null;
            else if (_singleton == null)
                _singleton = value;
            else if (_singleton != value)
            {
                Destroy(value);
                Debug.LogError($"There should only ever be one instance of {nameof(UIManager)}!");
            }
        }
    }
    private static UIManager _singleton;

    [SerializeField] private TextMeshProUGUI gameStateText;
    [SerializeField] private TextMeshProUGUI instructionText;
    [SerializeField] private Slider grappleCD;
    [SerializeField] private Slider glideCD;
    [SerializeField] private Image glideActive;
    [SerializeField] private Slider doubleJumpCD;
    [SerializeField] private LeaderboardItem[] leaderboardItems;

    public Player LocalPlayer;

    private void Awake()
    {
        Singleton = this;

        grappleCD.value = 0f;
        glideCD.value = 0f;
        doubleJumpCD.value = 0f;
    }

    private void Update()
    {
        if (LocalPlayer == null)
            return;

        grappleCD.value = LocalPlayer.GrappleCDFactor;
        doubleJumpCD.value = LocalPlayer.DoubleJumpCDFactor;

        glideActive.enabled = LocalPlayer.IsGliding;
        glideCD.value = LocalPlayer.IsGliding ? LocalPlayer.GlideCharge : LocalPlayer.GlideCDFactor;
    }

    private void OnDestroy()
    {
        if (Singleton == this)
            Singleton = null;
    }

    public void DidSetReady()
    {
        instructionText.text = "Waiting for other players to be ready...";
    }

    public void SetWaitUI(GameState newState, Player winner)
    {
        if (newState == GameState.Waiting)
        {
            if (winner == null)
            {
                gameStateText.text = "Waiting to Start";
                instructionText.text = "Press R when you're ready to begin!";
            }
            else
            {
                gameStateText.text = $"{winner.Name} Wins";
                instructionText.text = "Press R when you're ready to play again!";
            }
        }

        gameStateText.enabled = newState == GameState.Waiting;
        instructionText.enabled = newState == GameState.Waiting;
    }

    public void UpdateLeaderboard(KeyValuePair<Fusion.PlayerRef, Player>[] players)
    {
        for (int i = 0; i < leaderboardItems.Length; i++)
        {
            LeaderboardItem item = leaderboardItems[i];
            if (i < players.Length)
            {
                item.nameText.text = players[i].Value.Name;
                item.heightText.text = $"{players[i].Value.Score}m";
            }
            else
            {
                item.nameText.text = "";
                item.heightText.text = "";
            }
        }
    }

    [Serializable]
    private struct LeaderboardItem
    {
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI heightText;
    }
}
