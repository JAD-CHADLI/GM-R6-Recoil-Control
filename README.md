# GM R6 Recoil Control

![GM R6 Recoil Control](GM%20R6%20Recoil%20Control/Images/readme-banner.png)

![GM R6 Recoil Control](GM%20R6%20Recoil%20Control/Images/readme1-banner.png)

A Windows desktop tool for creating **per-operator, per-weapon recoil profiles** for Rainbow Six Siege.  
You can save multiple setups, bind them to keys, and trigger smooth mouse movements using a **Right-Click + Left-Click** combo – now with a **global Start/Stop hotkey** and extra features.

---

## ✨ Features

- 🎯 **Per-operator profiles**
  - Separate lists for **Attackers** and **Defenders**
  - Built-in profiles for all operators (Sledge, Ash, Jäger, Deimos, Ram, Thorn, etc.)
  - Each operator keeps its own saved recoil values, keybinds, and weapon selection

- 🔫 **Per-weapon recoil control**
  - Each operator has a list of **Primary** and **Secondary** weapons
  - You can choose which primary/secondary is active for that operator
  - Recoil values are stored **per weapon**, so you can fine-tune each gun separately

- ⚙️ **Multiple setups**
  - **Setup 1**  
    - Typically used for your **primary weapon**  
    - Stores Horizontal & Vertical speed + a hotkey
  - **Setup 2**  
    - Typically used for your **secondary / sidearm / second weapon**  
    - Also stores its own speeds + hotkey
  - **Special Setup 3 (Maestro only)**  
    - Extra setup meant for Maestro’s turret/camera recoil  
    - Has its own speeds + keybind, only visible when Maestro is selected

- ⌨️ **Hotkey-based switching**
  - Assign any key to Setup 1 / Setup 2 (and Setup 3 for Maestro)
  - Press the key in-game to instantly switch the **active setup** for the currently selected operator
  - The UI shows which setup is active and a summary of the saved recoil for each setup & weapon

- 🖱 **Mouse combo activation**
  - Toggle the tool **ON/OFF** with:
    - The **Start** button on the main page **or**
    - A **global Start/Stop hotkey** (configurable in the Tutorial page)
  - When active:
    - Hold **Right Mouse Button**
    - Then press and hold **Left Mouse Button**
    - The tool sends **relative mouse movement** using `SendInput`, so games see it as real input

- 🧠 **Global Start/Stop hotkey**
  - Set a dedicated **Start/Stop key** in the **Tutorial** page
  - Works globally (even in fullscreen), using `GetAsyncKeyState`
  - Press it once → start  
    Press again → stop

- 🖼 **Modern UI**
  - Dark themed WinForms UI
  - Operator cards with thumbnails and category tabs:
    - **Attackers** / **Defenders**
  - Search bar to quickly find operators
  - Detailed **Settings** page with:
    - Big operator preview image
    - **Movement** card with sliders + numeric input for Horizontal/Vertical speed
    - **Weapons** card:
      - Primary/Secondary weapon selection
      - Weapon images
    - **Setups & Keybinds** card:
      - Setup 1, Setup 2 and Setup 3 (Maestro) controls
      - Keybinds display
      - Saved recoil summary labels for each setup

- 🧪 **Tutorial page**
  - Built-in tutorial explaining how to:
    - Select operators and weapons
    - Tune recoil
    - Assign keybinds
    - Use the Start/Stop key
  - Also hosts:
    - **Global Start/Stop hotkey** controls
    - **Export settings**
    - **Import settings**
    - **RESET ALL** (reset every profile’s speeds & keybinds)

- 💾 **Persistent settings**
  - All speeds, weapon recoil values, keybinds, selected weapons and the global Start/Stop key are saved to `profiles.json`
  - Automatically loaded next time you run the app
  - You can **export** your settings to a separate `.json` file and **import** them on another PC

---

## 🚀 Quick Start

1. **Launch the app**
2. On the main page:
   - Choose **Attackers** or **Defenders**
   - Use the **search bar** or scroll to find an operator
   - Click the card to select it  
   - Click **Modify** to open the Settings page

3. **Pick your weapon**
   - In the **Weapons** card:
     - Choose the **Primary** weapon for Setup 1
     - Choose the **Secondary** weapon for Setup 2
   - The preview updates with the correct gun and its saved recoil values

4. **Tune movement**
   - In the **Movement** card:
     - Adjust **Horizontal speed** with the slider or numeric textbox
     - Adjust **Vertical speed** the same way
   - These values are what the tool will use when recoil is active

5. **Save setups**
   - **Setup 1**:
     - Click **Set Key 1**, press a key
     - Click **Save 1** to save speeds for the current weapon
   - **Setup 2**:
     - Click **Set Key 2**, press a key
     - Click **Save 2**
   - **Maestro only**:
     - Use **Setup 3** controls to save a third set of values (e.g. for his camera)

6. **Global Start/Stop key (optional but recommended)**
   - Go to the **Tutorial** page
   - Under “Start / Stop key”:
     - Click **Set key**
     - Press the key you want to use as global Start/Stop

7. **Use in-game**
   - On the main page, click **Start** (or press your global Start/Stop key)
   - In-game:
     - Hold **Right Mouse Button**
     - Then press and hold **Left Mouse Button**
     - Recoil control will kick in for the **currently active setup** of the selected operator
   - Press your setup keys (Setup 1 / 2 / 3) to switch recoil profiles on the fly

---

## 🧩 How It Works (Technical Overview)

- A `System.Windows.Forms.Timer` ticks every **20 ms**.
- On each tick it:
  - Reads global input state using `GetAsyncKeyState`:
    - Global Start/Stop hotkey
    - Mouse **Right Button** and **Left Button**
    - Setup keys for the current operator (Setup 1, Setup 2, and Setup 3 for Maestro)
  - Handles:
    - **Global toggle** (Start/Stop) via a hidden “global profile”
    - **Setup switching**: pressing a setup key loads that setup’s Horizontal/Vertical speeds (per-weapon if set)
- When the tool is active and the **Right + Left mouse combo** is pressed:
  - It accumulates horizontal & vertical deltas based on the current speeds
  - Sends them as **relative** mouse movement using `SendInput(MOUSEEVENTF_MOVE)`
  - Because the movement is relative, most games treat it as normal mouse input

All user-configurable data (profiles, weapon recoil values, keybinds, selected weapons, global hotkey) is stored in a single `profiles.json` file in the app directory.  
Export/Import just copy that configuration to/from another `.json`.

---

## 🔁 Reset, Export & Import

- **Reset profile** (Settings page)
  - Resets speeds, keybinds and weapon recoil for the **currently selected operator** only  
  - Operator name and images are not touched

- **RESET ALL** (Tutorial page)
  - Resets speeds, keybinds, per-weapon recoil and the global Start/Stop key for **all** operators
  - Deletes the `profiles.json` file so everything starts from clean defaults

- **Export settings**
  - Saves your current `profiles.json` to a user-chosen `.json` file  
  - You can share this file with friends or back it up

- **Import settings**
  - Loads a `.json` file and replaces all your current recoil/weapon/keybind settings
  - Automatically refreshes the UI and the global Start/Stop key to match the imported data

---

## ⚠️ Disclaimer 
> Use it **at your own risk** and **only** where it’s allowed  
> (e.g. offline, custom games, testing, or environments that explicitly permit such tools).

