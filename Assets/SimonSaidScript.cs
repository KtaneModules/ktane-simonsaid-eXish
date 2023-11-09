using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Text.RegularExpressions;
using System;

public class SimonSaidScript : MonoBehaviour {

    public KMAudio audio;
    public KMRuleSeedable ruleSeed;
    public KMBombInfo bomb;
    public KMColorblindMode colorblind;
    public KMSelectable[] buttons;
    public MeshRenderer[] btnRenderers;
    public Material[] darkMaterials;
    public Material[] litMaterials;
    public TextMesh[] cbTexts;

    enum OffsetType
    {
        None,
        Opposite,
        Clockwise,
        AntiClock,
    }

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

    private string[][] encodedInstsAll;
    private OffsetType[][] offsetPosPressesAll;

    void HandleRuleSeed()
    {
        var randomizer = ruleSeed != null ? ruleSeed.GetRNG() : new MonoRandom(1);
        if (randomizer.Seed == 1)
        {
            encodedInstsAll = new string[][] {
                new[] { "PL", "PU", "CG", "CR" }, // Color order from left to right, Red, Blue, Green, Yellow.
                new[] { "S1", "S1", "PD", "CB" }, // "PX", X is URDL positions relative to module.
                new[] { "S1", "S2", "S2", "S1" }, // "CX", X is the color RBGY.
                new[] { "S3", "S2", "S1", "S3" }, // "S#", # is the stage of which the button was correct on.
                new[] { "S4", "PU", "CY", "S2" }, // Since the button colors do not change upon a reset,
                new[] { "S3", "S5", "CG", "CY" }, // we do not need to get the color of the button pressed. (In respect to the original module.)
            };
            offsetPosPressesAll = new OffsetType[][] {
                new[] { OffsetType.None, OffsetType.None, OffsetType.None, OffsetType.None },
                new[] { OffsetType.Opposite, OffsetType.None, OffsetType.None, OffsetType.None },
                new[] { OffsetType.Clockwise, OffsetType.AntiClock, OffsetType.Clockwise, OffsetType.AntiClock },
                new[] { OffsetType.Clockwise, OffsetType.Clockwise, OffsetType.Clockwise, OffsetType.AntiClock },
                new[] { OffsetType.None, OffsetType.None, OffsetType.None, OffsetType.Opposite },
                new[] { OffsetType.None, OffsetType.Clockwise, OffsetType.None, OffsetType.None },
            };
        }
        else
        {
            var possibleRules = new string[][] {
                new[] { "P", "ULRD" },
                new[] { "C", "RBGY" },
                new[] { "S", "12345" },
            };
            var idxAllowedRulesPerStage = new List<int>[] {
                new List<int> { 0, 0, 0, 0, 1, 1, 1, 1 },
                new List<int> { 0, 0, 0, 1, 1, 1, 2, 2, 2 },
                new List<int> { 0, 0, 0, 1, 1, 1, 2, 2, 2, 2 },
                new List<int> { 0, 0, 0, 1, 1, 1, 2, 2, 2, 2, 2, 2 },
                new List<int> { 0, 0, 1, 1, 2, 2, 2, 2 },
                new List<int> { 0, 0, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2 },
            };
            encodedInstsAll = new string[6][];
            offsetPosPressesAll = new OffsetType[6][];
            for (var x = 0; x < 6; x++)
            {
                var curListShuffled = randomizer.ShuffleFisherYates(idxAllowedRulesPerStage[x].ToList()).Take(4);
                var curEncodedInst = new string[4];
                var curOffsetInst = new OffsetType[4];
                for (var y = 0; y < 4; y++)
                {
                    var newString = "";
                    var curIdxInst = curListShuffled.ElementAt(y);
                    var stageDetected = false;
                    for (var z = 0; z < possibleRules[curIdxInst].Length; z++)
                    {
                        var possibleItems = possibleRules[curIdxInst][z];
                        if (stageDetected)
                        {
                            possibleItems = possibleItems.Substring(0, x);
                            curOffsetInst[y] = new[] { OffsetType.Clockwise, OffsetType.AntiClock, OffsetType.None, OffsetType.Opposite }[randomizer.Next(0, 4)];
                        }
                        var pickedChr = possibleItems[randomizer.Next(0, possibleItems.Length)];
                        if (pickedChr == 'S')
                            stageDetected = true;
                        newString += pickedChr;
                    }
                    curEncodedInst[y] = newString;
                    //Debug.LogFormat("<Simon Said #{0}> {1}", moduleId, curEncodedInst[y]);
                }
                encodedInstsAll[x] = curEncodedInst;
                offsetPosPressesAll[x] = curOffsetInst;
            }

        }
        Debug.LogFormat("[Simon Said #{0}] Using rule seed {1} for the instructions.", moduleId, randomizer.Seed);
        Debug.LogFormat("<Simon Said #{0}> Instructions:", moduleId);
        for (var x = 0; x < 6; x++)
        {
            Debug.LogFormat("<Simon Said #{0}> Stage {1}:", moduleId, x + 1);
            for (var y = 0; y < 4; y++)
                Debug.LogFormat("<Simon Said #{0}> {1}: {2}", moduleId, colorNames[y], string.Format("{0} {1}", encodedInstsAll[x][y], offsetPosPressesAll[x][y].ToString()));
        }
    }

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
        HandleRuleSeed();
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
    void GenerateStageWithRuleSeed(int stage)
    {
        var colorFlashed = btnColors[flashingBtn];

        var obtainedInstruction = encodedInstsAll[stage][colorFlashed];
        var obtainedOffset = offsetPosPressesAll[stage][colorFlashed];
        var targetPos = -1;
        var _ndChr = obtainedInstruction[1];
        var debugString = "";
        switch (obtainedInstruction[0])
        {
            case 'P':
                {
                    var posObtained = "ULRD".IndexOf(_ndChr);
                    targetPos = posObtained;
                    debugString = string.Format("{0} button.", new[] { "top", "left", "right", "bottom" }[posObtained]);
                }
                break;
            case 'C':
                {
                    var colorObtained = "RBGY".IndexOf(_ndChr);
                    targetPos = btnColors.IndexOf(colorObtained);
                    debugString = string.Format("{0} button.", colorNames[colorObtained]);
                }
                break;
            case 'S':
                {
                    var stageObtained = "123456".IndexOf(_ndChr);
                    targetPos = correctBtnPresses[stageObtained];
                    debugString = string.Format("button pressed on stage {0}", stageObtained + 1);
                }
                break;
        }
        correctBtnPresses.Add(GetCorrectButtonPos(obtainedOffset, targetPos));
        switch (obtainedOffset)
        {
            case OffsetType.None:
                Debug.LogFormat("[Simon Said #{0}] Rule for this stage is to press the {1}", moduleId, debugString);
                break;
            case OffsetType.Opposite:
                Debug.LogFormat("[Simon Said #{0}] Rule for this stage is to press the button opposite from the {1}.", moduleId, debugString);
                break;
            case OffsetType.AntiClock:
                Debug.LogFormat("[Simon Said #{0}] Rule for this stage is to press the button 1 counter-clockwise from the {1}.", moduleId, debugString);
                break;
            case OffsetType.Clockwise:
                Debug.LogFormat("[Simon Said #{0}] Rule for this stage is to press the button 1 clockwise from the {1}.", moduleId, debugString);
                break;
        }
        Debug.LogFormat("[Simon Said #{0}] Correct button: {1}", moduleId, colorNames[btnColors[correctBtnPresses.Last()]]);
        Debug.LogFormat("[Simon Said #{0}] Expected sequence: {1}", moduleId, correctBtnPresses.Select(a => colorNames[btnColors[a]]).Join(", "));
    }
    void GenerateStage(int stage)
    {
        
        Debug.LogFormat("[Simon Said #{0}] ==== STAGE {1} ====", moduleId, stage + 1);
        flashingBtn = UnityEngine.Random.Range(0, 4);
        //flashingBtn = btnColors.IndexOf(0); // Debug purposes
        if (activated)
            flashStage = StartCoroutine(HandleStageFlash());
        Debug.LogFormat("[Simon Said #{0}] Flashing color: {1}", moduleId, colorNames[btnColors[flashingBtn]]);
        if (ruleSeed != null) { GenerateStageWithRuleSeed(stage); return; }
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
                        correctBtnPresses.Add(GetCorrectButtonPos(OffsetType.Opposite, correctBtnPresses[0]));
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
                        correctBtnPresses.Add(GetCorrectButtonPos(OffsetType.Clockwise, correctBtnPresses[0]));
                        break;
                    case 1:
                        correctBtnPresses.Add(GetCorrectButtonPos(OffsetType.AntiClock, correctBtnPresses[1]));
                        break;
                    case 2:
                        correctBtnPresses.Add(GetCorrectButtonPos(OffsetType.Clockwise, correctBtnPresses[1]));
                        break;
                    default:
                        correctBtnPresses.Add(GetCorrectButtonPos(OffsetType.AntiClock, correctBtnPresses[0]));
                        break;
                }
                break;
            case 3:
                switch (btnColors[flashingBtn])
                {
                    case 0:
                        correctBtnPresses.Add(GetCorrectButtonPos(OffsetType.Clockwise, correctBtnPresses[2]));
                        break;
                    case 1:
                        correctBtnPresses.Add(GetCorrectButtonPos(OffsetType.Clockwise, correctBtnPresses[1]));
                        break;
                    case 2:
                        correctBtnPresses.Add(GetCorrectButtonPos(OffsetType.Clockwise, correctBtnPresses[0]));
                        break;
                    default:
                        correctBtnPresses.Add(GetCorrectButtonPos(OffsetType.AntiClock, correctBtnPresses[2]));
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
                        correctBtnPresses.Add(GetCorrectButtonPos(OffsetType.Opposite, correctBtnPresses[1]));
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
                        correctBtnPresses.Add(GetCorrectButtonPos(OffsetType.Clockwise, correctBtnPresses[4]));
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

    int GetCorrectButtonPos(OffsetType type, int start)
    {
        switch (type)
        {
            case OffsetType.Opposite:
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
            case OffsetType.Clockwise:
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
            case OffsetType.AntiClock:
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
        return start;
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
        while (!activated) yield return true;
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
