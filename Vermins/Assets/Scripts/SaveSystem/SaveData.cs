using UnityEngine;

[System.Serializable]
public class SaveData
{
    public string sceneName;

    public float playerPosX;
    public float playerPosY;
    public float playerPosZ;

    public float playerRotX;
    public float playerRotY;
    public float playerRotZ;

    public float playerHealth;

    // Ian: o que o Player carrega. Save de antes destes campos abre com
    // tudo zerado, que e o mesmo que um jogo novo.
    public int playerPotions;
    public int playerGold;
    public int playerJewels;
    public int playerJewelsValue;
}