using Godot;

public partial class PauseUi : CanvasLayer
{
 [Export(PropertyHint.File, "*.tscn")]
 public string MainMenuScenePath = ""; // เว้นว่างได้ถ้าไม่มีเมนูหลัก

 [Export] public bool PauseAudio = false;     // เปิดแล้วจะ mute bus ตอนพัก
 [Export] public string AudioBusName = "Master";

 private Control _panel;   // node ชื่อ Panel แต่ type จริงคือ Control
 private Button _menuBtn;  // node ชื่อ MenuButton แต่ type จริงคือ Button
 private Control _subMenu;
 private BaseButton _resume, _quitGame;

 public override void _Ready()
 {
  GD.Print("[PauseUi] _Ready() start");

  // รับอินพุตเสมอ + ให้ลอยหน้า HUD
  ProcessMode = Node.ProcessModeEnum.Always;
  Layer = 100;

  // --- หาโหนด (ตาม Scene Tree ที่ส่งมา) ---
  _panel      = GetNode<Control>("Panel");
  _menuBtn    = GetNode<Button>("Panel/MenuButton");
  _subMenu    = GetNode<Control>("Panel/SubMenu");
  _resume     = GetNode<BaseButton>("Panel/SubMenu/ResumeButton");
  _quitGame   = GetNode<BaseButton>("Panel/SubMenu/QuitGameButton");

  // --- กันคลิกทะลุ + ซ้อนด้านบนใน canvas เดียวกัน ---
  _panel.MouseFilter = Control.MouseFilterEnum.Stop;
  _panel.ZIndex = 100;
  _subMenu.ZIndex = 101;

  // --- บังคับคุณสมบัติของปุ่มหลัก ---
  _menuBtn.Disabled = false;
  _menuBtn.FocusMode = Control.FocusModeEnum.All;
  _menuBtn.MouseFilter = Control.MouseFilterEnum.Stop;

  // --- ต่อสัญญาณ ---
  _menuBtn.ButtonDown += OnMenuDown;
  _menuBtn.Pressed    += OnMenuPressed;
  _menuBtn.GuiInput   += OnMenuGuiInput;

  _resume.Pressed     += OnResume;
  _quitGame.Pressed   += OnQuitGame;

  // ซ่อนเมนูย่อยตอนเริ่ม
  _subMenu.Visible = false;

  if (OS.HasFeature("web"))
   _quitGame.Visible = false;

  GD.Print("[PauseUi] Ready OK");
 }

 public override void _UnhandledInput(InputEvent e)
{
	// ✅ ถ้ามี HUD overlay โชว์อยู่ → ไม่ให้ PauseUI รับ input
	var hudCard = GetTree().Root.FindChild("HudCard", true, false) as Control;
	if (hudCard != null && hudCard.Visible)
		return;

	// Hotkey ทดสอบ: Enter/Space -> toggle เมนูย่อย
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

 // ---------- ตัวรับสัญญาณ ----------
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

 // ---------- เปิด/ปิดเมนูย่อย ----------
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

 private void OnQuitGame()
 {
  GetTree().Paused = false;
  SetAudioPaused(false);
  _subMenu.Visible = false;
  GetTree().ChangeSceneToFile("res://scenecheckpoint/checkpoint.tscn");
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
