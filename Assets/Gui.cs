using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Gui : MonoBehaviour
{
    public GameObject server_display_player;
    public GameObject proxy_player;

    public void OnToggleServerPlayer(bool enabled)
    {
        this.server_display_player.SetActive(enabled);
    }

    public void OnToggleProxyPlayer(bool enabled)
    {
        this.proxy_player.SetActive(enabled);
    }
}
