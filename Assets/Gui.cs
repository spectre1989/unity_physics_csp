using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Gui : MonoBehaviour
{
    public Logic logic;
    public GameObject server_display_player;
    public GameObject proxy_player;
    public Text packet_loss_label;

    public void OnToggleServerPlayer(bool enabled)
    {
        this.server_display_player.SetActive(enabled);
    }

    public void OnToggleProxyPlayer(bool enabled)
    {
        this.proxy_player.SetActive(enabled);
    }

    public void OnToggleCorrections(bool enabled)
    {
        this.logic.enable_corrections = enabled;
    }

    public void OnPacketLossSliderChanged(float value)
    {
        this.packet_loss_label.text = string.Format("Packet Loss - {0:F1}%", value);
        this.logic.packet_loss_chance = value;
    }
}
