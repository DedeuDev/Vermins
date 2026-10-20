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

    // Ian: o XP total do Player. O nivel sai dele pela curva do
    // CharacterLevel. Save de antes deste campo abre com 0, o nivel 1.
    public int playerXp;

    // Ian: os pontos livres de nivel que o jogador ja gastou, por
    // atributo. Os livres saem do nivel menos estes. Save de antes destes
    // campos abre com 0, ou seja, com todos os pontos ainda livres.
    public int playerPowerPoints;
    public int playerHastePoints;
    public int playerVitalityPoints;

    // Ian: o equipamento vestido, pelo nome do asset em Resources/Items.
    // ScriptableObject nao vai pro JSON, entao gravo o nome e o load acha
    // o asset de volta. Vazio e slot vazio. Save de antes destes campos
    // abre com null, que tambem e slot vazio.
    public string playerArma;
    public string playerArmadura;
    public string playerAnel;
}