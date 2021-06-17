using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Text.RegularExpressions;
using System;

public class SimonSaidScript : MonoBehaviour {

    public KMAudio audio;
    public KMBombInfo bomb;
    public KMColorblindMode colorblind;
    public KMSelectable[] buttons;
    public MeshRenderer[] btnRenderers;
    public Material[] darkMaterials;
    public Material[] litMaterials;
    public TextMesh[] cbTexts;

    private Coroutine flashStage;
    private Coroutine flashBtn;
    private string[] colorNames = { "Red", "Blue", "Green", "Yellow" };
    private string[] soundNames = { "beep4", "beep2", "beep1", "beep3" };
    private string[] pressNames = { "1st", "2nd", "3rd", "4th", "5th", "6th" };
    private List<int> btnColors = new List<int>{ 0, 1, 2, 3 };
    private List<int> correctBtnPresses = new List<int>();
    private int pressIndex;
    private int flashingBtn;
    private bool activated;
    private bool colorblindActive;
    private bool firstPress = true;

    static int moduleIdCounter = 1;
    int moduleId;
    private bool moduleSolved;

    void Awake()
    {
        moduleId = moduleIdCounter++;
        moduleSolved = false;
        foreach (KMSelectable obj in buttons)
        {
            KMSelectable pressed = obj;
            pressed.OnInteract += delegate () { PressButton(pressed); return false; };
        }
        GetComponent<KMBombModule>().OnActivate += OnActivate;
    }

    void Start () {
        btnColors = btnColors.Shuffle();
        for (int i = 0; i < 4; i++)
        {
            btnRenderers[i].material = darkMaterials[btnColors[i]];
            cbTexts[i].text = "";
        }
        Debug.LogFormat("[Simon Said #{0}] Colors of the buttons starting from the top and going clockwise: {1}, {2}, {3}, {4}", moduleId, colorNames[btnColors[0]], colorNames[btnColors[2]], colorNames[btnColors[3]], colorNames[btnColors[1]]);
    }

    void OnActivate()
    {
        GenerateStage(0);
        flashStage = StartCoroutine(HandleStageFlash());
        colorblindActive = colorblind.ColorblindModeActive;
        for (int i = 0; i < 4; i++)
        {
            if (colorblindActive)
                cbTexts[i].text = colorNames[btnColors[i]][0].ToString();
        }
        activated = true;
    }

    void PressButton(KMSelectable pressed)
    {
        if (activated != false)
        {
            pressed.AddInteractionPunch(0.75f);
            audio.PlaySoundAtTransform(soundNames[btnColors[Array.IndexOf(buttons, pressed)]], transform);
            if (firstPress == true)
                firstPress = false;
            if (flashStage != null)
            {
                StopCoroutine(flashStage);
                flashStage = null;
                for (int i = 0; i < 4; i++)
                    btnRenderers[i].material = darkMaterials[btnColors[i]];
            }
            if (flashBtn != null)
            {
                StopCoroutine(flashBtn);
                flashBtn = null;
                for (int i = 0; i < 4; i++)
                    btnRenderers[i].material = darkMaterials[btnColors[i]];
            }
            flashBtn = StartCoroutine(HandleButtonFlash(Array.IndexOf(buttons, pressed)));
            if (moduleSolved != true)
            {
                if (Array.IndexOf(buttons, pressed) == correctBtnPresses[pressIndex])
                {
                    pressIndex++;
                    if (pressIndex == correctBtnPresses.Count)
                    {
                        if (correctBtnPresses.Count == 6)
                        {
                            Debug.LogFormat("[Simon Said #{0}] Inputted sequence was correct. Module solved.", moduleId);
                            moduleSolved = true;
                            GetComponent<KMBombModule>().HandlePass();
                        }
                        else
                        {
                            Debug.LogFormat("[Simon Said #{0}] Inputted sequence was correct. Advancing to next stage...", moduleId);
                            pressIndex = 0;
                            GenerateStage(correctBtnPresses.Count);
                        }
                    }
                }
                else
                {
                    Debug.LogFormat("[Simon Said #{0}] The {1} input ({2}) was incorrect. Strike! Resetting back to stage 1...", moduleId, pressNames[pressIndex], colorNames[btnColors[Array.IndexOf(buttons, pressed)]]);
                    pressIndex = 0;
                    correctBtnPresses.Clear();
                    GetComponent<KMBombModule>().HandleStrike();
                    GenerateStage(0);
                }
            }
        }
    }

    void GenerateStage(int stage)
    {
        Debug.LogFormat("[Simon Said #{0}] ==== STAGE {1} ====", moduleId, stage + 1);
        flashingBtn = UnityEngine.Random.Range(0, 4);
        if (activated)
            flashStage = StartCoroutine(HandleStageFlash());
        Debug.LogFormat("[Simon Said #{0}] Flashing color: {1}", moduleId, colorNames[btnColors[flashingBtn]]);
        switch (stage)
        {
            case 0:
                switch (btnColors[flashingBtn])
                {
                    case 0:
                        correctBtnPresses.Add(1);
                        break;
                    case 1:
                        correctBtnPresses.Add(0);
                        break;
                    case 2:
                        correctBtnPresses.Add(btnColors.IndexOf(2));
                        break;
                    default:
                        correctBtnPresses.Add(btnColors.IndexOf(0));
                        break;
                }
                break;
            case 1:
                switch (btnColors[flashingBtn])
                {
                    case 0:
                        correctBtnPresses.Add(GetCorrectButtonPos(0, correctBtnPresses[0]));
                        break;
                    case 1:
                        correctBtnPresses.Add(correctBtnPresses[0]);
                        break;
                    case 2:
                        correctBtnPresses.Add(3);
                        break;
                    default:
                        correctBtnPresses.Add(btnColors.IndexOf(1));
                        break;
                }
                break;
            case 2:
                switch (btnColors[flashingBtn])
                {
                    case 0:
                        correctBtnPresses.Add(GetCorrectButtonPos(1, correctBtnPresses[0]));
                        break;
                    case 1:
                        correctBtnPresses.Add(GetCorrectButtonPos(2, correctBtnPresses[1]));
                        break;
                    case 2:
                        correctBtnPresses.Add(GetCorrectButtonPos(1, correctBtnPresses[1]));
                        break;
                    default:
                        correctBtnPresses.Add(GetCorrectButtonPos(2, correctBtnPresses[0]));
                        break;
                }
                break;
            case 3:
                switch (btnColors[flashingBtn])
                {
                    case 0:
                        correctBtnPresses.Add(GetCorrectButtonPos(1, correctBtnPresses[2]));
                        break;
                    case 1:
                        correctBtnPresses.Add(GetCorrectButtonPos(1, correctBtnPresses[1]));
                        break;
                    case 2:
                        correctBtnPresses.Add(GetCorrectButtonPos(1, correctBtnPresses[0]));
                        break;
                    default:
                        correctBtnPresses.Add(GetCorrectButtonPos(2, correctBtnPresses[2]));
                        break;
                }
                break;
            case 4:
                switch (btnColors[flashingBtn])
                {
                    case 0:
                        correctBtnPresses.Add(correctBtnPresses[3]);
                        break;
                    case 1:
                        correctBtnPresses.Add(0);
                        break;
                    case 2:
                        correctBtnPresses.Add(btnColors.IndexOf(3));
                        break;
                    default:
                        correctBtnPresses.Add(GetCorrectButtonPos(0, correctBtnPresses[1]));
                        break;
                }
                break;
            default:
                switch (btnColors[flashingBtn])
                {
                    case 0:
                        correctBtnPresses.Add(correctBtnPresses[2]);
                        break;
                    case 1:
                        correctBtnPresses.Add(GetCorrectButtonPos(1, correctBtnPresses[4]));
                        break;
                    case 2:
                        correctBtnPresses.Add(btnColors.IndexOf(2));
                        break;
                    default:
                        correctBtnPresses.Add(btnColors.IndexOf(3));
                        break;
                }
                break;
        }
        Debug.LogFormat("[Simon Said #{0}] Correct button: {1}", moduleId, colorNames[btnColors[correctBtnPresses.Last()]]);
        Debug.LogFormat("[Simon Said #{0}] Expected sequence: {1}", moduleId, correctBtnPresses.Join(", ").Replace("0", colorNames[btnColors[0]]).Replace("1", colorNames[btnColors[1]]).Replace("2", colorNames[btnColors[2]]).Replace("3", colorNames[btnColors[3]]));
    }

    int GetCorrectButtonPos(int type, int start)
    {
        switch (type)
        {
            case 0:
                switch (start)
                {
                    case 0:
                        return 3;
                    case 1:
                        return 2;
                    case 2:
                        return 1;
                    default:
                        return 0;
                }
            case 1:
                switch (start)
                {
                    case 0:
                        return 2;
                    case 1:
                        return 0;
                    case 2:
                        return 3;
                    default:
                        return 1;
                }
            default:
                switch (start)
                {
                    case 0:
                        return 1;
                    case 1:
                        return 3;
                    case 2:
                        return 0;
                    default:
                        return 2;
                }
        }
    }

    IEnumerator HandleStageFlash()
    {
        while (true)
        {
            yield return new WaitForSeconds(2f);
            if (firstPress == false)
                audio.PlaySoundAtTransform(soundNames[btnColors[flashingBtn]], transform);
            btnRenderers[flashingBtn].material = litMaterials[btnColors[flashingBtn]];
            yield return new WaitForSeconds(0.3f);
            btnRenderers[flashingBtn].material = darkMaterials[btnColors[flashingBtn]];
        }
    }

    IEnumerator HandleButtonFlash(int pos)
    {
        btnRenderers[pos].material = litMaterials[btnColors[pos]];
        yield return new WaitForSeconds(0.3f);
        btnRenderers[pos].material = darkMaterials[btnColors[pos]];
    }

    //twitch plays
    #pragma warning disable 414
    private readonly string TwitchHelpMessage = @"!{0} press <R/B/G/Y> [Presses the red, blue, green, or yellow button] | !{0} colorblind [Toggles colorblind mode] | Presses are chainable with or without spaces";
    #pragma warning restore 414
    IEnumerator ProcessTwitchCommand(string command)
    {
        if (Regex.IsMatch(command, @"^\s*colorblind\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            yield return null;
            if (!colorblindActive)
            {
                for (int i = 0; i < 4; i++)
                    cbTexts[i].text = colorNames[btnColors[i]][0].ToString();
            }
            else
            {
                for (int i = 0; i < 4; i++)
                    cbTexts[i].text = "";
            }
            colorblindActive = !colorblindActive;
            yield break;
        }
        string[] parameters = command.Split(' ');
        if (Regex.IsMatch(parameters[0], @"^\s*press\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            yield return null;
            if (parameters.Length == 1)
            {
                yield return "sendtochaterror Please specify at least one button to press!";
            }
            else
            {
                string[] valids = { "R", "B", "G", "Y" };
                string allPresses = "";
                for (int i = 1; i < parameters.Length; i++)
                {
                    for (int j = 0; j < parameters[i].Length; j++)
                    {
                        if (!valids.Contains(parameters[i][j].ToString().ToUpper()))
                        {
                            yield return "sendtochaterror!f The specified button to press '" + parameters[i][j] + "' is invalid!";
                            yield break;
                        }
                        allPresses += parameters[i][j];
                    }
                }
                for (int i = 0; i < allPresses.Length; i++)
                {
                    int pressInd = Array.IndexOf(valids, allPresses[i].ToString().ToUpper());
                    buttons[btnColors.IndexOf(pressInd)].OnInteract();
                    yield return new WaitForSeconds(0.2f);
                }
            }
            yield break;
        }
    }

    IEnumerator TwitchHandleForcedSolve()
    {
        int ct = 7 - correctBtnPresses.Count;
        for (int i = 0; i < ct; i++)
        {
            int start = 0;
            int end = correctBtnPresses.Count;
            if (i == 0)
                start = pressIndex;
            for (int j = start; j < end; j++)
            {
                buttons[correctBtnPresses[j]].OnInteract();
                yield return new WaitForSeconds(0.2f);
            }
        }
    }
}
