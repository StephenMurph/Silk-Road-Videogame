using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;

public class EventPopupUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject root;

    [Header("Content")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Image eventImage;

    [Header("Buttons")]
    [SerializeField] private GameObject okButtonRoot;
    [SerializeField] private Button okButton;
    [SerializeField] private TMP_Text okButtonText;

    [SerializeField] private GameObject option1Root;
    [SerializeField] private Button option1Button;
    [SerializeField] private TMP_Text option1Text;

    [SerializeField] private GameObject option2Root;
    [SerializeField] private Button option2Button;
    [SerializeField] private TMP_Text option2Text;
    

    private Action onOkPressed;
    private Action onOption1Pressed;
    private Action onOption2Pressed;

    public bool IsShowing => root != null && root.activeSelf;

    private void Awake()
    {
        if (!root)
            root = gameObject;

        if (okButton)
            okButton.onClick.AddListener(HandleOkPressed);

        if (option1Button)
            option1Button.onClick.AddListener(HandleOption1Pressed);

        if (option2Button)
            option2Button.onClick.AddListener(HandleOption2Pressed);

        root.SetActive(false);
    }

    public void ShowSimpleEvent(
        string title,
        string description,
        Sprite imageSprite,
        string okText = "OK",
        Action okCallback = null)
    {
        onOkPressed = okCallback;
        onOption1Pressed = null;
        onOption2Pressed = null;

        ApplyContent(title, description, imageSprite);

        if (okButtonText)
            okButtonText.text = okText;

        if (okButtonRoot)
            okButtonRoot.SetActive(true);

        if (option1Root)
            option1Root.SetActive(false);

        if (option2Root)
            option2Root.SetActive(false);

        root.SetActive(true);
    }

    public void ShowChoiceEvent(
        string title,
        string description,
        Sprite imageSprite,
        string option1Label,
        Action option1Callback,
        string option2Label,
        Action option2Callback)
    {
        onOkPressed = null;
        onOption1Pressed = option1Callback;
        onOption2Pressed = option2Callback;

        ApplyContent(title, description, imageSprite);

        if (okButtonRoot)
            okButtonRoot.SetActive(false);

        if (option1Root)
            option1Root.SetActive(true);

        if (option1Text)
            option1Text.text = option1Label;

        if (option2Root)
            option2Root.SetActive(true);

        if (option2Text)
            option2Text.text = option2Label;

        root.SetActive(true);
    }

    public void Hide()
    {
        root.SetActive(false);
        onOkPressed = null;
        onOption1Pressed = null;
        onOption2Pressed = null;
    }

    private void ApplyContent(string title, string description, Sprite imageSprite)
    {
        if (titleText)
            titleText.text = title;

        if (descriptionText)
            descriptionText.text = description;

        if (eventImage)
        {
            eventImage.sprite = imageSprite;
            eventImage.enabled = imageSprite != null;
        }
    }

    private void HandleOkPressed()
    {
        root.SetActive(false);
        var callback = onOkPressed;
        onOkPressed = null;
        callback?.Invoke();
    }

    private void HandleOption1Pressed()
    {
        root.SetActive(false);
        var callback = onOption1Pressed;
        onOption1Pressed = null;
        onOption2Pressed = null;
        callback?.Invoke();
    }

    private void HandleOption2Pressed()
    {
        root.SetActive(false);
        var callback = onOption2Pressed;
        onOkPressed = null;
        onOption2Pressed = null;
        callback?.Invoke();
    }
    
    public void SetOption1Interactable(bool interactable)
    {
        if (option1Button != null)
            option1Button.interactable = interactable;
    }

    public void SetOption2Interactable(bool interactable)
    {
        if (option2Button != null)
            option2Button.interactable = interactable;
    }
}