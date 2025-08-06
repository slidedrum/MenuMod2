using BepInEx.Logging;
using Pigeon.Movement;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MenuMod2
{
    public static class MenuMod2Manager
    {
        internal static new ManualLogSource Logger;
        public static List<MenuMod2Menu> allMenus;
        public static MenuMod2Menu currentMenu = null;
    }

    public class MenuMod2Menu
    {
        public List<MM2Button> buttons;
        public GameObject menuCanvas;
        public string menuName;
        public MenuMod2Menu parrentMenu;
        public MM2Button thisButton = null;
        public List<MenuMod2Menu> subMenus;
        public MenuMod2Menu(string indetifier, MenuMod2Menu _parrentMenu = null)
        {
            menuName = indetifier;
            if (MenuMod2Manager.allMenus == null)
            {
                MenuMod2Manager.allMenus = new List<MenuMod2Menu>();
            }
            foreach (var menu in MenuMod2Manager.allMenus)
            {
                if (menu.menuName == indetifier)
                {
                    throw new Exception($"Menu with name {indetifier} already exists.");
                }
            }
            MenuMod2Manager.allMenus.Add(this);
            buttons = new List<MM2Button>();
            menuCanvas = new GameObject("menuCanvas");
            GameObject.DontDestroyOnLoad(menuCanvas);

            Canvas canvas = menuCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            menuCanvas.AddComponent<CanvasScaler>();
            menuCanvas.AddComponent<GraphicRaycaster>();

            if (_parrentMenu != null)
            {
                parrentMenu = _parrentMenu;
                parrentMenu.subMenus ??= new List<MenuMod2Menu>();
                parrentMenu.subMenus.Add(this);
                MenuMod2.Logger.LogDebug($"Creating menu {indetifier} under {_parrentMenu.menuName}");
                thisButton = parrentMenu.addButton(indetifier, () => { this.Open(); });
                this.addButton("Back", () => { parrentMenu.Open(); }).changeColour(Color.grey).changeSuffix($" ({parrentMenu.menuName})").changePrefix($"[{menuName}]\n");
            }
            else if (indetifier == "Main Menu")
            {
                MenuMod2.Logger.LogDebug($"Creating menu {indetifier}");
                this.addButton("Close", () => { this.Close(); }).changeColour(Color.red);
                parrentMenu = null;
            }
        }
        public void Open()
        {
            MenuMod2.Logger.LogDebug($"Opening menu: {menuName ?? "unkown"}");
            if (MenuMod2Manager.currentMenu != null && MenuMod2Manager.currentMenu != this)
            {
                MenuMod2Manager.currentMenu.Close();
            }
            MenuMod2Manager.currentMenu = this;
            UnityEngine.Cursor.visible = true;
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            foreach (var button in buttons)
            {
                button.show();
            }
        }
        public void Close()
        {
            if (MenuMod2Manager.currentMenu != this)
            {
                MenuMod2.Logger.LogWarning($"Attempted to close menu \"{menuName}\" that wasn't open.  This should not happen");
                return;
            }
            MenuMod2.Logger.LogDebug($"Closing menu: {menuName}");
            MenuMod2Manager.currentMenu = null;

            if (Player.LocalPlayer != null && Player.LocalPlayer.PlayerLook.EnableMenuCamera == 0)
            {
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
            }
            foreach (var button in buttons)
            {
                button.hide();
            }
        }
        public MenuMod2Menu hasMenu(string menuName)
        {
            if (this.menuName == menuName)
            {
                return this;
            }
            if (subMenus != null)
            {
                foreach (var subMenu in subMenus)
                {
                    if (subMenu.menuName == menuName)
                    {
                        return subMenu;
                    }
                }
            }
            return null;
        }
        public MM2Button addButtonBackup(string text, UnityAction callback)
        {
            MM2Button button = new MM2Button(this, new Vector2(0, 0), text, callback, menuCanvas);
            buttons.Add(button);
            arrangeButtons();
            return button;
        }
        public MM2Button addButton(string text, UnityAction callback)
        {
            MM2Button button = new MM2Button(this, new Vector2(0, 0), text, null, menuCanvas);

            button.createButton();
            button.SetCallback(callback);

            buttons.Add(button);
            arrangeButtons();
            return button;
        }
        public void destroy()
        {
            var tempMenus = new List<MenuMod2Menu>(MenuMod2Manager.allMenus);
            foreach (var menu in tempMenus)
            {
                if (menu.menuName == this.menuName)
                {
                    MenuMod2Manager.allMenus.Remove(menu);
                }
            }
            if (MenuMod2Manager.currentMenu == this)
            {
                var backButton = this.buttons.FirstOrDefault(b => b.name == "back");
                if (backButton == null)
                {
                    this.Close();
                }
                else
                {
                    backButton.button.onClick.Invoke();
                }
            }
            var tempParrentButtons = new List<MM2Button>(parrentMenu?.buttons ?? new List<MM2Button>());
            foreach (var button in tempParrentButtons)
            {
                if (button.name == this.thisButton.name)
                {
                    parrentMenu.buttons.Remove(button);
                }
            }
            var buttonsToRemove = new List<MM2Button>(buttons);
            foreach (var button in buttonsToRemove)
            {
                GameObject.Destroy(button.buttonObj);
                buttons.Remove(button);
            }
            var subMenusToDestroy = subMenus ?? new List<MenuMod2Menu>();
            foreach (var subMenu in subMenusToDestroy)
            {
                subMenu.destroy();
            }

            GameObject.Destroy(menuCanvas);
        }
        public bool removeButton(string buttonName)
        {
            var buttonToRemove = buttons.FirstOrDefault(b => b.name == buttonName);
            if (buttonToRemove != null)
            {
                buttons.Remove(buttonToRemove);
                GameObject.Destroy(buttonToRemove.buttonObj);
                arrangeButtons();
                return true;
            }
            return false;
        }
        public void arrangeButtons()
        {
            int buttonSizeX = 200;
            int buttonSizeY = 50;
            int spacing = 5;

            Vector2 spiralOrigin = new Vector2(0, 0);

            int currentGridX = 0;
            int currentGridY = 0;

            int stepX = 1;
            int stepY = 0;

            int stepsInCurrentDirection = 1;
            int stepsTaken = 0;
            int directionChangeCount = 0;

            for (int i = 0; i < buttons.Count; i++)
            {

                Vector2 buttonPosition = new Vector2(
                    currentGridX * (buttonSizeX + spacing),
                    currentGridY * -(buttonSizeY + spacing)
                );
                buttons[i].move(buttonPosition + spiralOrigin);

                currentGridX += stepX;
                currentGridY += stepY;
                stepsTaken++;

                if (stepsTaken == stepsInCurrentDirection)
                {
                    stepsTaken = 0;

                    int oldStepX = stepX;
                    stepX = -stepY;
                    stepY = oldStepX;

                    directionChangeCount++;

                    if (directionChangeCount % 2 == 0)
                    {
                        stepsInCurrentDirection++;
                    }
                }
            }
        }
    }
    public class MM2Button
    {
        public GameObject buttonObj;
        public MenuMod2Menu menu;
        public string name;
        public string prefix;
        public string suffix;
        public GameObject canvas;
        public Vector2 pos;
        public Button button;
        public MM2Button(MenuMod2Menu _menu, Vector2 screenPos, string text, UnityAction callback, GameObject menuCanvas)
        {
            menu = _menu;
            name = text;
            pos = screenPos;
            canvas = menuCanvas;
            prefix = string.Empty;
            suffix = string.Empty;
        }
        public MM2Button createButton()
        {
            MenuMod2.Logger.LogDebug($"Creating button: {name} in menu {menu.menuName}");
            buttonObj = new GameObject("MenuButton");
            buttonObj.transform.SetParent(canvas.transform, false);
            RectTransform rectTransform = buttonObj.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(200, 50);
            rectTransform.anchoredPosition = pos;

            button = buttonObj.AddComponent<Button>();
            Image image = buttonObj.AddComponent<Image>();
            image.color = Color.white;

            GameObject textObj = new GameObject("ButtonText");
            textObj.transform.SetParent(buttonObj.transform, false);
            Text buttonText = textObj.AddComponent<Text>();
            buttonText.text = prefix + name + suffix;
            buttonText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            buttonText.color = Color.black;
            buttonText.alignment = TextAnchor.MiddleCenter;
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            hide();
            return this;
        }
        public MM2Button hide()
        {
            buttonObj.SetActive(false);
            return this;
        }
        public MM2Button show()
        {
            buttonObj.SetActive(true);
            return this;
        }
        public MM2Button updateText()
        {
            Text buttonText = buttonObj.GetComponentInChildren<Text>();
            if (buttonText != null)
            {
                buttonText.text = prefix + name + suffix;
            }
            else
            {
                MenuMod2.Logger.LogWarning("Button text component not found, cannot update text.");
            }
            return this;
        }
        public MM2Button changeColour(Color newColor)
        {
            Image buttonImage = buttonObj.GetComponent<Image>();
            if (buttonImage != null)
            {
                buttonImage.color = newColor;
            }
            else
            {
                MenuMod2.Logger.LogWarning("Button image component not found, cannot change color.");
            }
            return this;
        }
        public MM2Button changeName(string newName)
        {
            name = newName;
            updateText();
            return this;
        }
        public MM2Button changePrefix(string newPrefix)
        {
            prefix = newPrefix;
            updateText();
            return this;
        }
        public MM2Button changeSuffix(string newSuffix)
        {
            suffix = newSuffix;
            updateText();
            return this;
        }
        public MM2Button move(Vector2 newPos)
        {
            RectTransform rectTransform = buttonObj.GetComponent<RectTransform>();
            rectTransform.anchoredPosition = newPos;
            return this;
        }
        public void SetCallback(UnityAction callback)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(callback);
        }
    }

}
