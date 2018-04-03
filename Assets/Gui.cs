using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Gui : MonoBehaviour
{
    public Logic logic;
    public GameObject server_display_player;
    public GameObject proxy_player;

    public Toggle corrections_toggle;
    public Toggle server_player_toggle;
    public Toggle proxy_player_toggle;
    public Slider packet_loss_slider;
    public Text packet_loss_label;
    public Slider latency_slider;
    public Text latency_label;
    public Slider snapshot_rate_slider;
    public Text snapshot_rate_label;

    public void Start()
    {
        this.corrections_toggle.isOn = true;
        this.server_player_toggle.isOn = false;
        this.proxy_player_toggle.isOn = false;
        this.packet_loss_slider.value = this.logic.packet_loss_chance;
        this.latency_slider.value = this.logic.latency;
        this.snapshot_rate_slider.value = Mathf.Log(this.logic.server_snapshot_rate, 2.0f);
    }

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

    public void OnLatencySliderChanged(float value)
    {
        this.latency_label.text = string.Format("Latency - {0}ms", (int)(value * 1000.0f));
        this.logic.latency = value;
    }

    public void OnSnapshotRateSliderChanged(float value)
    {
        uint rate = (uint)Mathf.Pow(2, value);
        this.snapshot_rate_label.text = string.Format("Snapshot Rate - {0}hz", 64/rate);
        this.logic.server_snapshot_rate = rate;
    }
}
