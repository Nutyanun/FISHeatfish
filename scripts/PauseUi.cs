using Godot;                                           // ใช้คลาสจากเอนจิน Godot (CanvasLayer, Control, Button, InputEvent, ฯลฯ)

public partial class PauseUi : CanvasLayer            // UI พักเกม (Pause) ลอยทับเกม เพราะสืบทอดจาก CanvasLayer
{
 [Export(PropertyHint.File, "*.tscn")]
 public string MainMenuScenePath = ""; // เว้นว่างได้ถ้าไม่มีเมนูหลัก   // พาธซีนหน้าเมนูหลัก (ถ้ามี)

 [Export] public bool PauseAudio = false;     // เปิดแล้วจะ mute bus ตอนพัก  // ถ้า true จะปิดเสียงใน bus ที่กำหนดเมื่อ pause
 [Export] public string AudioBusName = "Master";                               // ชื่อ Audio Bus ที่จะ mute/unmute

 private Control _panel;   // node ชื่อ Panel แต่ type จริงคือ Control     // แผงครอบปุ่ม pause ทั้งหมด
 private Button _menuBtn;  // node ชื่อ MenuButton แต่ type จริงคือ Button   // ปุ่มหลักเพื่อเปิด/ปิดเมนูย่อย
 private Control _subMenu;                                                     // คอนเทนเนอร์ของเมนูย่อย (Resume / Quit)
 private BaseButton _resume, _quitGame;                                        // ปุ่ม Resume และ Quit ในเมนูย่อย

 public override void _Ready()                                                 // เรียกเมื่อโหนดพร้อมใช้งาน
 {
  GD.Print("[PauseUi] _Ready() start");                                        // ล็อกเริ่มต้น

  // รับอินพุตเสมอ + ให้ลอยหน้า HUD
  ProcessMode = Node.ProcessModeEnum.Always;                                   // ให้ประมวลผลแม้เกม pause (เพื่อจับอินพุต)
  Layer = 100;                                                                 // ชั้นการวาดสูง เพื่อให้อยู่เหนือ HUD/Control อื่นใน Canvas เดียวกัน

  // --- หาโหนด (ตาม Scene Tree ที่ส่งมา) ---
  _panel      = GetNode<Control>("Panel");                                     // หา Panel (ต้องมีในซีน)
  _menuBtn    = GetNode<Button>("Panel/MenuButton");                           // หา MenuButton ใต้ Panel
  _subMenu    = GetNode<Control>("Panel/SubMenu");                             // หา SubMenu ใต้ Panel
  _resume     = GetNode<BaseButton>("Panel/SubMenu/ResumeButton");             // หา ResumeButton ใต้ SubMenu
  _quitGame   = GetNode<BaseButton>("Panel/SubMenu/QuitGameButton");           // หา QuitGameButton ใต้ SubMenu

  // --- กันคลิกทะลุ + ซ้อนด้านบนใน canvas เดียวกัน ---
  _panel.MouseFilter = Control.MouseFilterEnum.Stop;                           // บล็อกเมาส์ไม่ให้คลิกทะลุ Panel ไปโดนฉากข้างหลัง
  _panel.ZIndex = 100;                                                         // เอา Panel ไว้ชั้นสูง
  _subMenu.ZIndex = 101;                                                       // SubMenu อยู่สูงกว่า Panel เล็กน้อย

  // --- บังคับคุณสมบัติของปุ่มหลัก ---
  _menuBtn.Disabled = false;                                                   // เปิดให้คลิกได้
  _menuBtn.FocusMode = Control.FocusModeEnum.All;                              // ให้รับโฟกัสทั้งคีย์บอร์ด/เมาส์/เกมแพด
  _menuBtn.MouseFilter = Control.MouseFilterEnum.Stop;                         // ปุ่มไม่ให้คลิกทะลุ

  // --- ต่อสัญญาณ ---
  _menuBtn.ButtonDown += OnMenuDown;                                           // จับเหตุการณ์ตอนปุ่มถูกกดลง (ก่อนปล่อย)
  _menuBtn.Pressed    += OnMenuPressed;                                        // จับเหตุการณ์กดแล้วปล่อย (pressed)
  _menuBtn.GuiInput   += OnMenuGuiInput;                                       // จับอินพุต GUI โดยตรงบนปุ่ม (เช่นคลิกซ้าย)

  _resume.Pressed     += OnResume;                                             // ต่อสัญญาณปุ่ม Resume
  _quitGame.Pressed   += OnQuitGame;                                           // ต่อสัญญาณปุ่ม Quit

  // ซ่อนเมนูย่อยตอนเริ่ม
  _subMenu.Visible = false;                                                    // เริ่มต้นไม่โชว์เมนูย่อย

  if (OS.HasFeature("web"))                                                    // ถ้ารันบนแพลตฟอร์ม web
   _quitGame.Visible = false;                                                  // ซ่อนปุ่ม Quit (เพราะออกแอปจริงไม่ได้)

  GD.Print("[PauseUi] Ready OK");                                              // ล็อกจบการเตรียม
 }

 public override void _UnhandledInput(InputEvent e)                            // จับอินพุตที่ยังไม่มีใครจัดการ (เหมาะกับปุ่มลัด)
{
// ถ้ามี HUD overlay โชว์อยู่ → ไม่ให้ PauseUI รับ input
var hudCard = GetTree().Root.FindChild("HudCard", true, false) as Control; // มองหา HUD overlay ชื่อ HudCard (เชิงลึก)
if (hudCard != null && hudCard.Visible)                                    // ถ้า HUD ขึ้นทับอยู่
return;                                                                 // ข้ามไม่ให้ PauseUI ตอบสนอง (กันชนกับหน้าจอผลลัพธ์)

// Hotkey ทดสอบ: Enter/Space -> toggle เมนูย่อย
if (e.IsActionPressed("ui_accept"))                                         // ถ้ากดปุ่มยืนยัน (เช่น Enter/Space)
{
GD.Print("[PauseUi] ui_accept toggle");                                 // ล็อก
ToggleSubMenuImmediate();                                               // สลับแสดง/ซ่อนเมนูย่อย
}

  // ESC -> Pause/Unpause
  if (e.IsActionPressed("ui_cancel"))                                          // กด ESC
  {
   if (e is InputEventKey k && k.Echo) return;                                 // ถ้าเป็นคีย์ repeat (กดค้าง) ให้เมิน
   bool paused = !GetTree().Paused;                                            // toggle สถานะ pause
   GetTree().Paused = paused;                                                  // ตั้งค่าหยุด/เดินเกม
   SetAudioPaused(paused);                                                     // จัดการเสียงตามสถานะ
   if (!paused) _subMenu.Visible = false;                                      // ถ้ากลับมาเล่น → ซ่อนเมนูย่อย
   else _menuBtn.GrabFocus();                                                  // ถ้าเพิ่ง pause → โฟกัสที่ปุ่มเมนู
   GD.Print("[PauseUi] ESC -> ", paused ? "PAUSED" : "PLAY");                  // ล็อกสถานะ
  }

  // คลิกนอก Panel -> ซ่อนเมนูย่อย
  if (_subMenu.Visible && e is InputEventMouseButton mb && mb.Pressed)         // ถ้าเมนูย่อยเปิดอยู่ และคลิกเมาส์
  {
   if (!_panel.GetGlobalRect().HasPoint(mb.GlobalPosition))                    // ถ้าคลิกนอกกรอบ Panel
   {
_subMenu.Visible = false;                                                  // ซ่อนเมนูย่อย
GD.Print("[PauseUi] Click outside -> hide submenu");                       // ล็อกการซ่อน
   }
  }
 }

 // ---------- ตัวรับสัญญาณ ----------
 private void OnMenuDown()                                                     // ถูกเรียกตอนปุ่มถูกกดลง (ก่อนปล่อย)
 {
  GD.Print("[PauseUi] ButtonDown");                                            // ล็อก
  ToggleSubMenuImmediate();                                                    // สลับแสดง/ซ่อนเมนูย่อยทันที
 }

 private void OnMenuPressed()                                                  // ถูกเรียกตอนปุ่มกด-ปล่อยสมบูรณ์
 {
  GD.Print("[PauseUi] Pressed");                                               // ล็อก
  ToggleSubMenuImmediate();                                                    // สลับเมนูย่อย
 }

 private void OnMenuGuiInput(InputEvent ev)                                     // รับอินพุต GUI บนปุ่ม (เช่นคลิกซ้าย)
 {
  if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left) // หากเป็นคลิกซ้ายที่เพิ่งกดลง
  {
   GD.Print("[PauseUi] GuiInput on MenuButton");                               // ล็อก
   ToggleSubMenuImmediate();                                                   // สลับเมนูย่อย
  }
 }

 // ---------- เปิด/ปิดเมนูย่อย ----------
 private void ToggleSubMenuImmediate()                                          // ฟังก์ชันสลับเมนูย่อยทันที
 {
  bool show = !_subMenu.Visible;                                               // คำนวณค่าสถานะใหม่ (กลับค่าปัจจุบัน)
  _subMenu.Visible = show;                                                     // ตั้งค่าให้แสดง/ซ่อน

  if (show)                                                                     // ถ้ากำลังจะแสดงเมนูย่อย
  {
   if (!GetTree().Paused) GetTree().Paused = true;                              // บังคับให้เกมเข้าสู่โหมด pause
   SetAudioPaused(true);                                                        // ถ้าตั้งให้ pause audio → ปิดเสียง bus
   _resume.GrabFocus();                                                         // โฟกัสไปที่ปุ่ม Resume (รองรับกด Enter ต่อ)
  }

  GD.Print("[PauseUi] Toggle -> SubMenu.Visible=", _subMenu.Visible);           // ล็อกสถานะปัจจุบันของเมนูย่อย
 }

 // ---------- ปุ่มย่อย ----------
 private void OnResume()                                                        // กดปุ่ม Resume
 {
  GetTree().Paused = false;                                                     // ปลด pause
  SetAudioPaused(false);                                                        // เปิดเสียงกลับ
  _subMenu.Visible = false;                                                     // ซ่อนเมนูย่อย
  GD.Print("[PauseUi] Resume");                                                 // ล็อก
 }

 private void OnQuitGame()                                                      // กดปุ่ม QuitGame
 {
  GetTree().Paused = false;                                                     // ปลด pause ก่อนเปลี่ยนฉาก (กันค้าง)
  SetAudioPaused(false);                                                        // เปิดเสียงกลับ
  _subMenu.Visible = false;                                                     // ซ่อนเมนูย่อยเพื่อความเนียน
  GetTree().ChangeSceneToFile("res://scenecheckpoint/checkpoint.tscn");         // ไปยังซีน checkpoint (ทางออกจากเกมหลักของโปรเจ็กต์นี้)
  GD.Print("[PauseUi] QuitGame");                                               // ล็อก
 }

 // ---------- คุมเสียงตอนพัก ----------
 private void SetAudioPaused(bool paused)                                       // เปิด/ปิด mute ของ bus ตามสถานะ pause
 {
  if (!PauseAudio) return;                                                      // ถ้าไม่ได้เปิดฟีเจอร์นี้ ไม่นับ
  int bus = AudioServer.GetBusIndex(AudioBusName);                              // หา index ของ bus จากชื่อ
  if (bus >= 0) AudioServer.SetBusMute(bus, paused);                            // ถ้าหาเจอ → สั่ง mute/unmute ตาม paused
 }
}
