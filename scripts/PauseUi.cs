using Godot;

public partial class PauseUi : CanvasLayer
{
	[Export(PropertyHint.File, "*.tscn")]
	public string MainMenuScenePath = ""; // เว้นว่างได้ถ้าไม่มีเมนูหลัก

	[Export] public bool PauseAudio = false;     // เปิดแล้วจะ mute bus ตอนพัก
	[Export] public string AudioBusName = "Master";

	private Panel _panel;
	private BaseButton _menuBtn, _resume, _quitToMenu, _quitGame;
	private Control _subMenu;

	public override void _Ready()
	{
		GD.Print("[PauseUi] _Ready() start");

		// รับอินพุตเสมอ + ให้ลอยหน้า HUD
		ProcessMode = Node.ProcessModeEnum.Always;
		Layer = 100;

		// --- หาโหนด ---
		_panel      = GetNode<Panel>("Panel");
		_menuBtn    = GetNode<BaseButton>("Panel/MenuButton");
		_subMenu    = GetNode<Control>("Panel/SubMenu");
		_resume     = GetNode<BaseButton>("Panel/SubMenu/ResumeButton");
		_quitToMenu = GetNode<BaseButton>("Panel/SubMenu/QuitToMenuButton");
		_quitGame   = GetNode<BaseButton>("Panel/SubMenu/QuitGameButton");

		// --- กันคลิกทะลุ + ซ้อนด้านบนใน canvas เดียวกัน ---
		_panel.MouseFilter = Control.MouseFilterEnum.Stop;
		_panel.ZIndex = 100;
		_subMenu.ZIndex = 101;

		// --- บังคับคุณสมบัติของปุ่มหลัก ---
		_menuBtn.Disabled = false;
		_menuBtn.FocusMode = Control.FocusModeEnum.All;
		_menuBtn.MouseFilter = Control.MouseFilterEnum.Stop;
		// _menuBtn.ActionMode = ...   // << ลบ/อย่าตั้งค่านี้ (เป็นสาเหตุ error)

		// --- ต่อสัญญาณหลายชั้น (กันพลาด) ---
		_menuBtn.ButtonDown += OnMenuDown;        // เมื่อกดลง
		_menuBtn.Pressed    += OnMenuPressed;     // ปกติ
		_menuBtn.GuiInput   += OnMenuGuiInput;    // fallback ที่ตัวปุ่ม
		_panel.GuiInput     += OnPanelGuiInput;   // fallback ที่ panel (คลิกโดนขอบ)

		_resume.Pressed     += OnResume;
		_quitToMenu.Pressed += OnQuitToMenu;
		_quitGame.Pressed   += OnQuitGame;

		// ซ่อนเมนูย่อยตอนเริ่ม
		_subMenu.Visible = false;

		if (OS.HasFeature("web"))
			_quitGame.Visible = false;
		if (string.IsNullOrEmpty(MainMenuScenePath))
			_quitToMenu.Visible = false;

		GD.Print("[PauseUi] Ready: CanvasLayer=", Layer,
				 ", PanelZ=", _panel.ZIndex, ", SubMenuZ=", _subMenu.ZIndex);
	}

	public override void _UnhandledInput(InputEvent e)
	{
		// Hotkey ทดสอบ: Enter/Space (ui_accept) -> toggle เมนูย่อย
		if (e.IsActionPressed("ui_accept"))
		{
			GD.Print("[PauseUi] ui_accept toggle");
			ToggleSubMenuImmediate();
		}

		// ESC -> Pause/Unpause
		if (e.IsActionPressed("ui_cancel"))
		{
			if (e is InputEventKey k && k.Echo) return;
			bool paused = !GetTree().Paused;
			GetTree().Paused = paused;
			SetAudioPaused(paused);
			if (!paused) _subMenu.Visible = false;
			else _menuBtn.GrabFocus();
			GD.Print("[PauseUi] ESC -> ", paused ? "PAUSED" : "PLAY");
		}

		// คลิกนอก Panel -> ซ่อนเมนูย่อย
		if (_subMenu.Visible && e is InputEventMouseButton mb && mb.Pressed)
		{
			if (!_panel.GetGlobalRect().HasPoint(mb.GlobalPosition))
			{
				_subMenu.Visible = false;
				GD.Print("[PauseUi] Click outside -> hide submenu");
			}
		}
	}

	// ---------- ตัวรับสัญญาณหลายชั้น ----------
	private void OnMenuDown()
	{
		GD.Print("[PauseUi] ButtonDown");
		ToggleSubMenuImmediate();
	}

	private void OnMenuPressed()
	{
		GD.Print("[PauseUi] Pressed");
		ToggleSubMenuImmediate();
	}

	private void OnMenuGuiInput(InputEvent ev)
	{
		if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
		{
			GD.Print("[PauseUi] GuiInput on MenuButton");
			ToggleSubMenuImmediate();
		}
	}

	private void OnPanelGuiInput(InputEvent ev)
	{
		if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
		{
			if (_menuBtn.GetGlobalRect().HasPoint(mb.GlobalPosition))
			{
				GD.Print("[PauseUi] Panel fallback -> MenuButton");
				ToggleSubMenuImmediate();
			}
		}
	}

	// ---------- เปิด/ปิดเมนูย่อยแบบตรงๆ ----------
	private void ToggleSubMenuImmediate()
	{
		bool show = !_subMenu.Visible;
		_subMenu.Visible = show;

		if (show)
		{
			if (!GetTree().Paused) GetTree().Paused = true;
			SetAudioPaused(true);
			_resume.GrabFocus();
		}

		GD.Print("[PauseUi] Toggle -> SubMenu.Visible=", _subMenu.Visible);
	}

	// ---------- ปุ่มย่อย ----------
	private void OnResume()
	{
		GetTree().Paused = false;
		SetAudioPaused(false);
		_subMenu.Visible = false;
		GD.Print("[PauseUi] Resume");
	}

	private void OnQuitToMenu()
	{
		GetTree().Paused = false;
		SetAudioPaused(false);
		_subMenu.Visible = false;
		if (!string.IsNullOrEmpty(MainMenuScenePath))
			GetTree().ChangeSceneToFile(MainMenuScenePath);
		GD.Print("[PauseUi] QuitToMenu");
	}

	private void OnQuitGame()
	{
		GetTree().Paused = false;
		SetAudioPaused(false);
		_subMenu.Visible = false;
		GetTree().Quit();
		GD.Print("[PauseUi] QuitGame");
	}

	// ---------- คุมเสียงตอนพัก ----------
	private void SetAudioPaused(bool paused)
	{
		if (!PauseAudio) return;
		int bus = AudioServer.GetBusIndex(AudioBusName);
		if (bus >= 0) AudioServer.SetBusMute(bus, paused);
	}
}
