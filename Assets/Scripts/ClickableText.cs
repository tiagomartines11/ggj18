﻿using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public struct NiceWord
{
    public NiceWord(int i, string w, int b, int l) { word = w; index = i; beginIndex = b; lastIndex = l; }
    public string word;
    public int index, beginIndex, lastIndex;
}

public class ClickableText : MonoBehaviour, IPointerDownHandler
{
    public Camera _camera;
    private Text _text;
    private List<ActionBlock> list;
    private int _score = 0;
    public GameObject optionsPanel;
    public Button actionButton;
    public TweetWindow tweetWindow;
    public Text actionText;
    public Text pickText;
    public Sprite[] buttonVisuals;
    public Meter meter;
    public RectTransform liveLine;

    private List<Button> buttons;

    void Start()
    {
        _text = GetComponent<Text>();

        loadLevel(PlayerProgression.currentLevel);
        _text.text = _text.text.Replace("<b>", "<b><color=#f1f1f2ff>").Replace("</b>", "</color></b>");

        actionText.text = "CHOOSE A WORD";
        pickText.enabled = false;

        buttons = new List<Button>();
    }

    public string ReplaceAt(string input, int index, char newChar)
    {
        char[] chars = input.ToCharArray();
        chars[index] = newChar;
        return new string(chars);
    }

    void Update()
    {
        var textGen = _text.cachedTextGenerator;
        for (int i = 1; i < textGen.characterCount - 1; ++i)
        {
            try
            {
                if (i == 0 || i == _text.text.Length - 2) continue;
                if (_text.text[i] != '<' || _text.text[i + 1] != '/' || _text.text[i + 2] != 'c') continue;
            }
            catch (Exception) { continue; }

            Vector2 wordBottomRight = transform.TransformPoint(new Vector2(textGen.verts[i * 4 + 2].position.x, textGen.verts[i * 4 + 2].position.y));

            if (wordBottomRight.y > liveLine.offsetMin.y)
            {
                var word = GetWordAtIndex(i - 1);
                var block = list[word.index];
                if (block.done) continue;
                block.done = true;
                _score += block.score;
                meter.SetScore(_score);
                if (block.feedback != "") {
                    tweetWindow.ScheduleTweet(new Tweet(block.feedback, block.score));
                }
                if (_score <= -30)
                {
                    failLevel();
                }
            }
        }

        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            if (buttons.Count > 0) buttons[0].onClick.Invoke();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            if (buttons.Count > 1) buttons[1].onClick.Invoke();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            if (buttons.Count > 2) buttons[2].onClick.Invoke();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            if (buttons.Count > 3) buttons[3].onClick.Invoke();
        }

    }

    void OnDrawGizmos()
    {
        var text = GetComponent<Text>();
        var textGen = text.cachedTextGenerator;
        var prevMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        for (int i = 0; i < textGen.characterCount; ++i)
        {
            Vector2 locUpperLeft = new Vector2(textGen.verts[i * 4].position.x, textGen.verts[i * 4].position.y);
            Vector2 locBottomRight = new Vector2(textGen.verts[i * 4 + 2].position.x, textGen.verts[i * 4 + 2].position.y);

            Vector3 mid = (locUpperLeft + locBottomRight) / 2.0f;
            Vector3 size = locBottomRight - locUpperLeft;

            Gizmos.DrawWireCube(mid, size);
        }
        Gizmos.matrix = prevMatrix;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        eventData.Use();
        int index = GetIndexOfClick(_camera.ScreenPointToRay(eventData.position));
        if (index != -1)
        {
            var niceWord = GetWordAtIndex(index);
            if (niceWord.index >= 0)
            {
                var block = list[niceWord.index];
                buttons.Clear();
                foreach (Transform child in optionsPanel.transform) GameObject.Destroy(child.gameObject);
                actionText.text = niceWord.word.ToUpper();
                pickText.enabled = true;
                int i = 0;
                foreach (var l in block.options)
                {
                    var b = GameObject.Instantiate(actionButton);
                    buttons.Add(b);
                    b.transform.SetParent(optionsPanel.transform);
                    b.name = l.param;
                    b.GetComponentInChildren<Text>().text = l.name;
                    b.GetComponentInChildren<Image>().sprite = buttonVisuals[i++];
                    b.onClick.AddListener(
                        () => {
                            buttons.Clear();
                            foreach (Transform child in optionsPanel.transform) GameObject.Destroy(child.gameObject);
                            actionText.text = "CHOOSE A WORD";
                            pickText.enabled = false;
                            _text.text= _text.text.Remove(niceWord.beginIndex - 20, niceWord.lastIndex - niceWord.beginIndex + 20).Insert(niceWord.beginIndex - 20, "<b><color=#00ff00ff>" + l.param);
                            block.score = l.deltaScore;
                            block.feedback = l.feedback;
                            }
                        );
                }
            }
        }
    }

    int GetIndexOfClick(Ray ray)
    {
        Ray localRay = new Ray(
          transform.InverseTransformPoint(ray.origin),
          transform.InverseTransformDirection(ray.direction));

        Vector3 localClickPos =
          localRay.origin +
          localRay.direction / localRay.direction.z * (transform.localPosition.z - localRay.origin.z);

        Debug.DrawRay(transform.TransformPoint(localClickPos), Vector3.up / 10, Color.red, 20.0f);

        var textGen = _text.cachedTextGenerator;
        for (int i = 0; i < textGen.characterCount; ++i)
        {
            Vector2 locUpperLeft = new Vector2(textGen.verts[i * 4].position.x, textGen.verts[i * 4].position.y);
            Vector2 locBottomRight = new Vector2(textGen.verts[i * 4 + 2].position.x, textGen.verts[i * 4 + 2].position.y);

            if (localClickPos.x >= locUpperLeft.x &&
             localClickPos.x <= locBottomRight.x &&
             localClickPos.y <= locUpperLeft.y &&
             localClickPos.y >= locBottomRight.y
             )
            {
                return i;
            }
        }

        return -1;
    }

    NiceWord GetWordAtIndex(int index)
    {
        int begIndex = -1;
        int marker = index;
        while (begIndex == -1)
        {
            marker--;
            if (marker < 0 || (_text.text[marker] == '>' && _text.text[marker-1] == 'b'))
            {
                return new NiceWord(-1, "", -1, -1);
            }
            else if (_text.text[marker] == '>' && _text.text[marker-1] == 'f')
            {
                begIndex = marker;
                break;
            }
        }

        int lastIndex = -1;
        marker = index;
        while (lastIndex == -1)
        {
            marker++;
            if (marker > _text.text.Length - 1)
            {
                return new NiceWord(-1, "", -1, -1);
            }
            else if (_text.text[marker] == '<')
            {
                lastIndex = marker;
                break;
            }
        }

        string source = _text.text.Substring(0, lastIndex);
        int count = -2;
        foreach (char c in source) if (c == '<') count++;

        return new NiceWord(count/4, _text.text.Substring(begIndex + 1, lastIndex - begIndex - 1), begIndex + 1, lastIndex);
    }

    void failLevel()
    {
        SceneManager.LoadScene("GameOver");
    }
    void completeLevel()
    {
        PlayerProgression.currentLevelScore = _score;
        SceneManager.LoadScene("LevelComplete");
    }
    void loadLevel(int n)
    {

        _text = GetComponent<Text>();

        list = new List<ActionBlock>();
        if (n == 0)
        {
            _text.text = "The presenter of the show HARU wears a <b>red</b> <b>suit</b> and roars:\n\nPRESENTER: \n<i> “Welcome to the final game of Slippery <b>Stairs</b>! Give it up for YUI and KAITO, everyone!!!” </i>\n\nThe whole audience <b>claps</b>. AUDIENCE#1 stands up out of <b>excitement</b>, clapping. His face is painted in Yui’s team’s color.\n\nYUI and KAITO both wear <b>vinyl</b> and protection on their <b>elbows</b>, <b>knees</b> and hands. YUI’s costume is <b>green</b>. KAITO’s is <b>orange</b>. \n\nPRESENTER:\n<i>”Let’s see who will make it to the top of the stairs!\n[mumbles to his tiny microphone]\nof course we know that no one will -- like always”</i>\n\nThe audience gasps and <b>laughs</b>.\n\nAUDIENCE #2 jumps up and down waving an <b>orange</b> <b>flag</b>.\n\nAUDIENCE #2:\n<i>“Go Kaitoooo! Whohoo! [whispering] i love you”</i>\n\nKAITO puts his fists up victoriously and <b>laughs</b>.\n\nYUI squints her eyes at KAITO and takes her position at the first step of the <b>staircase</b>. \n\nPRESENTER:\n<i>”On your mark!\nGet ready!!”</i>\n\n*drum rolls*\n\nPRESENTER:\n<i>“Go!”</i>\n\nYui and Kaito slowly but steadily crawl up the stairs one by one. \n\nAUDIENCE #3, in Kawaii Harajuku style <b>pink</b> <b>stockings</b>, eats a <b>baby blue</b> Japanese cheesecake.\n\nKaito slips down the stairs. \n\nPRESENTER:\n<i>“<b>Orange</b> is down!”</i>\n\nYui slips too because of Kaito’s falling impact. <b>Vinyl</b> doesn’t seem to be helping them.\n\nPRESENTER:\n<i> “Whoops! They’re all down!”</i>\n\nEveryone <b>laughs</b>.\n\nAUDIENCE#3 throws the rest of the cake at PRESENTER.\n\nEveryone <b>laughs</b>.\n\n------\n";
            list.Add(new ActionBlock(-5, "What was this 'red' nonsense?", new Replacement[] { new Replacement("violet", "violet", 5, ""), new Replacement("pink", "pink", -10, "Look at how that men is dressed, disgusting"), new Replacement("change to cyan", "cyan", -10, "Look at how that men is dressed, disgusting"), new Replacement("purple", "purple", -10, "Look at how that men is dressed, disgusting") }));
            list.Add(new ActionBlock(0, "", new Replacement[] { new Replacement("stocking", "stocking", 0, ""), new Replacement("skirt", "skirt", 0, ""), new Replacement("change to skirt", "skirt", 0, ""), new Replacement("strip that suit away", "thong", 0, "That thong is gonna trend #trend #trending") }));
            list.Add(new ActionBlock(0, "", new Replacement[] { new Replacement("slopes", "Slopes", 0, ""), new Replacement("stockings", "Stockings", -10, "That's just offensive."), new Replacement("", "", 0, "") }));
            list.Add(new ActionBlock(0, "", new Replacement[] { new Replacement("cries", "cries", 0, ""), new Replacement("change to laughs", "laughs", -10, "WHAT? Why would the audience laugh?? #disgusted"), new Replacement("smiles awkwardly", "smiles awkwardly", 0, "") }));
            list.Add(new ActionBlock(0, "", new Replacement[] { new Replacement("boredom", "boredom", -10, "Oh great! Now bored people can be freely shown on TV? #BoycottAlienCable"), new Replacement("joy", "joy", 0, ""), new Replacement("pressure", "pressure", 0, "") }));
            list.Add(new ActionBlock(-15, "For the big bang’s sake! i’m watching this show with my infants. #StopVinyl", new Replacement[] { new Replacement("stocking", "stockings", -20, "WHAT! IS THAT A STOCKING ON LIVE TV? #offended"), new Replacement("salmon skin", "salmon skin", 10, "That salmon skin is amazing. #want #need #salmon #skin #noFilter #waitWhat"), new Replacement("just remove the frigging vinyl", "no clothing", 5, "YAY! #goNudeOrGoHome") }));
            list.Add(new ActionBlock(0, "", new Replacement[] { new Replacement("arms", "arms", 0, ""), new Replacement("hands", "hands", 0, ""), new Replacement("face", "face", 0, "") }));
            list.Add(new ActionBlock(0, "", new Replacement[] { new Replacement("legs", "legs", 0, ""), new Replacement("food", "food", 0, ""), new Replacement("butts", "butts", 0, "I'm ok with this #loveButts") }));
            list.Add(new ActionBlock(-10, "Why gotta be green?", new Replacement[] { new Replacement("violet", "violet", 0, ""), new Replacement("pink", "pink", -10, "PINK on TV? #boycottAlienCable"), new Replacement("cyan", "cyan", -10, "Ugh. This color hurts my eyes"), new Replacement("purple", "purple", -10, "Ugh. This color hurts my eyes") }));
            list.Add(new ActionBlock(0, "", new Replacement[] { new Replacement("violet", "violet", 0, ""), new Replacement("pink", "pink", -10, "What the hell is this color."), new Replacement("cyan", "cyan", -10, "Ugh. This color hurts my eyes"), new Replacement("purple", "purple", -10, "terrible!! almost feels like a h*man show") }));
            list.Add(new ActionBlock(-15, "Look at the way they open their mouths and make those horrible noises #stopLaughter #AlienRightsNow", new Replacement[] { new Replacement("screams", "screams", 0, ""), new Replacement("cries", "cries", 0, ""), new Replacement("nods", "nods in approval", 10, ""), new Replacement("chuckles", "chuckles", -10, "Ugh. This color hurts my eyes") }));
            list.Add(new ActionBlock(0, "", new Replacement[] { new Replacement("violet", "violet", 0, ""), new Replacement("pink", "pink", -10, "PINK on TV? #boycottAlienCable"), new Replacement("cyan", "cyan", -10, "Ugh. This color hurts my eyes"), new Replacement("purple", "purple", -10, "Ugh. This color hurts my eyes") }));
            list.Add(new ActionBlock(-10, "That's offensive! We are #OneNation! [b]NO FLAGS[/b]", new Replacement[] { new Replacement("hamburger", "hambuger?", 0, ""), new Replacement("regular meaningless cloth", "regular meaningless cloth", 0, ""), new Replacement("vinyl jacket", "vinyl jacket", -10, "") }));
            list.Add(new ActionBlock(-15, "Laughing is disgusting. #ProtectOurChildren #StopLaughin", new Replacement[] { new Replacement("screams", "screams", 0, ""), new Replacement("cries", "cries", 0, ""), new Replacement("nods", "nods in approval", 10, ""), new Replacement("chuckles", "chuckles", -10, "Ugh. This color hurts my eyes") }));
            list.Add(new ActionBlock(0, "", new Replacement[] { new Replacement("ladder", "ladder", 0, ""), new Replacement("slope", "slope", 0, ""), new Replacement("stockings", "stockings", -15, "That makes absolutely no sense. #WTF") }));
            list.Add(new ActionBlock(-10, "PINK on TV? #boycottAlienCable", new Replacement[] { new Replacement("violet", "violet", 0, ""), new Replacement("golden", "golden", -10, "Is [b]golden[/b] even a color?"), new Replacement("cyan", "cyan", -10, "Ugh. This color hurts my eyes"), new Replacement("purple", "purple", -10, "Ugh. This color hurts my eyes") }));
            list.Add(new ActionBlock(-15, "WHY DO HUMANS USE THIS DISGUSTING STOCKINGS #WHY", new Replacement[] { new Replacement("shoes", "shoes", 0, ""), new Replacement("cape", "cape", 0, ""), new Replacement("vinyl pants", "vinyl pants", -10, "VINYL PANTS? Adding insult to injury!") }));
            list.Add(new ActionBlock(10, "AlienCable cares for my baby. Support #babyBlue ! It's the #bestColor", new Replacement[] { new Replacement("violet", "violet", 0, ""), new Replacement("pink", "pink", -10, "PINK on TV? #boycottAlienCable"), new Replacement("cyan", "cyan", -10, "Ugh. This color hurts my eyes #StopAlienCable"), new Replacement("change to purple", "purple", -10, "Ugh. This color hurts my eyes") }));
            list.Add(new ActionBlock(0, "", new Replacement[] { new Replacement("violet", "violet", 0, ""), new Replacement("pink", "pink", -10, "PINK on TV? #boycottAlienCable"), new Replacement("cyan", "cyan", -10, "Ugh. This color hurts my eyes"), new Replacement("purple", "purple", -10, "Ugh. This color hurts my eyes") }));
            list.Add(new ActionBlock(-15, "For the big bang’s sake! i’m watching this show with my infants. #StopVinyl", new Replacement[] { new Replacement("stocking", "stockings", -20, "WHAT! IS THAT A STOCKING ON LIVE TV? #offended"), new Replacement("salmon skin", "salmon skin", 10, "That salmon skin is amazing. #want #need #salmon #skin #noFilter #waitWhat"), new Replacement("just remove the frigging vinyl", "no clothing", 5, "YAY! #goNudeOrGoHome") }));
            list.Add(new ActionBlock(-15, "Laughing is disgusting. #ProtectOurChildren #StopLaughin", new Replacement[] { new Replacement("screams", "screams", 0, ""), new Replacement("cries", "cries", 0, "Weird, but at least they're not laughing!"), new Replacement("nods", "nods in approval", 5, "What a civilized audience."), new Replacement("chuckles", "chuckles", -10, "That's even worse than laughing! ") }));
            list.Add(new ActionBlock(-15, "Those bizarre mouth sounds. My child will never recover from the horror. #callMyLawyer", new Replacement[] { new Replacement("screams", "screams", 0, ""), new Replacement("cries", "cries", 0, ""), new Replacement("nods", "nods in approval", 5, "#That's it, nice and polite"), new Replacement("chuckles", "chuckles", -10, "Oh great! Now CHUCKLING can be freely shown on TV? #BoycottAlienCable") }));
        }
        if (n == 1)
        {
            list.Add(new ActionBlock(-10, "PICKING FAVORITES UH, ALIENCABLE??", new Replacement[] { new Replacement("delightful", "delightful", 0, ""), new Replacement("famous", "famous", 0, ""), new Replacement("neutral", "neutral", 5, "Yes, neutral things are my fav.. i mean, are the best!") }));
            list.Add(new ActionBlock(-10, "What is this? Brainwashing? #stopBranding", new Replacement[] { new Replacement("all pet stores", "all pet stores combined", 5, "Beautiful! All pet stores are one! #emotional"), new Replacement("the whole population", "the whole alien population", 0, "WHAT? Oh, well ok."), new Replacement("AlienCable", "AlienCable", -10, "AlienCable merchandising in AlienCable? Outrageous!") }));
            list.Add(new ActionBlock(-10, "Filthy Human companies being broadcast in my home? I don't think so... #destroyAlienCable", new Replacement[] { new Replacement("Generic", "generic", 5, "generic rocks!"), new Replacement("just biscuits", "just biscuit", 5, "My kids are loving this show and just biscuit. #FamilyTime"), new Replacement("delicious", "delicious", 5, "delicious indeed!") }));
            list.Add(new ActionBlock(-10, "What the hell is poochypoo? IS THIS MERCHANDISING??? #disgusted", new Replacement[] { new Replacement("any shampoo", "Any", 5, "Totally agree. 'Any shampoo is the best shampoo!'"), new Replacement("homemade", "Homemade", 5, "Yes, for DIY moms as myself #make #your #own #shampoo"), new Replacement("Alien Brand (tm)", "AlienBrand(tm)", -10, "A TV show that talks about brands??? IS THIS CAPITALISM??") }));
            list.Add(new ActionBlock(-10, "Is it a dog or a slave? #freeTheDogs", new Replacement[] { new Replacement("Toto", "Toto", -10, "Is it a dog or a slave? #freeTheDogs"), new Replacement("Boo Boo", "Boo Boo", -10, "Is it a dog or a slave? #freeTheDogs"), new Replacement("Martha Sinclair", "Martha Sinclair", 5, "So cute") }));
            list.Add(new ActionBlock(-10, "Oh next thing we know they will force us to buy the brands that show on TV... #noBrands", new Replacement[] { new Replacement("beautiful", "beautiful", 10, "Beautiful diamond indeed! #iWantOne"), new Replacement("Alien Brand (tm)", "AlienBrand(tm)", -10, "A TV show that talks about brands??? IS THIS CAPITALISM??"), new Replacement("Cervical", "Cervical", 10, "Gotta love cervical collars!") }));
            list.Add(new ActionBlock(-10, "Is it a dog or a slave? #freeTheDogs", new Replacement[] { new Replacement("Gurl", "Gurl", -10, "Is it a dog or a slave? #freeTheDogs"), new Replacement("Tina", "Tina", -10, "Is it a dog or a slave? #freeTheDogs"), new Replacement("Victoria Whiterspoon", "Victoria Whiterspoon", 5, "They even look alike, i love it") }));
            list.Add(new ActionBlock(-10, "Is it a dog or a slave? #freeTheDogs", new Replacement[] { new Replacement("Gurl", "GURL", -10, "Is it a dog or a slave? #freeTheDogs"), new Replacement("Tina", "TINA", -10, "Is it a dog or a slave? #freeTheDogs"), new Replacement("Victoria Whiterspoon", "VICTORIA WHITHERSPOON", 5, "They even look alike, i love it") }));
            list.Add(new ActionBlock(-10, "Is it a dog or a slave? #freeTheDogs", new Replacement[] { new Replacement("Babe", "Babe", -10, "Is it a dog or a slave? #freeTheDogs"), new Replacement("Fifi Carrington", "Fifi Carrington", -10, ""), new Replacement("Rex", "Rex", -10, "Is it a dog or a slave? #freeTheDogs") }));
            list.Add(new ActionBlock(-10, "Is it a dog or a slave? #freeTheDogs", new Replacement[] { new Replacement("any pet store", "some pet store", 15, ""), new Replacement("Alien Brand (tm)", "AlienBrand(tm)", -10, "A TV show that talks about brands??? IS THIS CAPITALISM??"), new Replacement("AlienCable", "AlienCable", -10, "AlienCable merchandising in AlienCable? Outrageous!") }));
            list.Add(new ActionBlock(-10, "Is it a dog or a slave? #freeTheDogs", new Replacement[] { new Replacement("Bebecita", "Bebecita", -10, "Is it a dog or a slave? #freeTheDogs"), new Replacement("Baby girl", "Baby girl", -10, "Is it a dog or a slave? #freeTheDogs"), new Replacement("Madison", "Madison", 10, "Oh what a cute dog-owner pair <3") }));
            list.Add(new ActionBlock(-10, "Filthy Human companies being broadcast in my home? I don't think so... #destroyAlienCable", new Replacement[] { new Replacement("Generic", "generic", 5, "generic rocks!"), new Replacement("just biscuits", "just biscuit", 5, "My kids are loving this show and just biscuit. #FamilyTime"), new Replacement("delicious", "delicious", 5, "delicious indeed!") }));
            list.Add(new ActionBlock(-10, "Is it a dog or a slave? #freeTheDogs", new Replacement[] { new Replacement("Bebecita", "Bebecita", -10, "Is it a dog or a slave? #freeTheDogs"), new Replacement("Baby girl", "Baby girl", -10, "Is it a dog or a slave? #freeTheDogs"), new Replacement("Madison", "Madison", 10, "Oh what a cute dog-owner pair <3") }));
            list.Add(new ActionBlock(-10, "Is it a dog or a slave? #freeTheDogs", new Replacement[] { new Replacement("Bebecita", "Bebecita", -10, "Is it a dog or a slave? #freeTheDogs"), new Replacement("Baby girl", "Baby girl", -10, "Is it a dog or a slave? #freeTheDogs"), new Replacement("Madison", "Madison", 10, "Oh what a cute dog-owner pair <3") }));
            list.Add(new ActionBlock(0, "", new Replacement[] { new Replacement("Madison", "Madison", -10, "Wait, her, her dog AND her son are all called Madison?"), new Replacement("Doggy", "Doggy", -10, "She gave her son a slave-name? I'm so confused and offended"), new Replacement("Rex", "Rex", -10, "She gave her son a slave-name? I'm so confused and offended") }));
            list.Add(new ActionBlock(-10, "What the hell is poochypoo? IS THIS MERCHANDISING??? #disgusted", new Replacement[] { new Replacement("any shampoo", "any", 5, "Totally agree. 'Any shampoo is the best shampoo!'"), new Replacement("homemade", "Homemade", 5, "Yes, for DIY moms as myself #make #your #own #shampoo"), new Replacement("Alien Brand (tm)", "AlienBrand(tm)", -10, "A TV show that talks about brands??? IS THIS CAPITALISM??") }));
        }
        _text.text = _text.text.Replace("<b>", "<b><color=#f1f1f2ff>").Replace("</b>", "</color></b>");

    }
}
