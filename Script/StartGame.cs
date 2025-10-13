using Godot;                                     // ใช้ API ของ Godot (Node2D, Label, Button, ResourceLoader ฯลฯ)
using System;                                    // ใช้ .NET พื้นฐาน (DateTime ฯลฯ)
using System.Globalization;                      // ใช้ CultureInfo / DateTimeStyles สำหรับ parse/format วันที่

public partial class StartGame : Node2D          // ซีนหน้าเริ่มเกม/เมนูหลัก สืบทอดจาก Node2D
{
	[Export] private NodePath PlayerInfoPath;    // [Export] ให้ตั้ง path ของ Label แสดงข้อมูลผู้เล่นได้จาก Inspector

	// ปุ่ม (ตั้งผ่าน Inspector ได้ หรือปล่อยให้หาโดยชื่อในซีน)
	[Export] private NodePath StartButtonPath;   // [Export] path ปุ่มเริ่มเล่น
	[Export] private NodePath HighscoreButtonPath; // [Export] path ปุ่มไปหน้า High Score

	// ← แก้ให้เป็น path ของโปรเจกต์คุณจริง ๆ
	private const string PLAY_SCENE      = "res://scenecheckpoint/checkpoint.tscn"; // พาธไฟล์ซีน "เล่นเกม"
	private const string HIGHSCORE_SCENE = "res://SceneHighSc/HighScore.tscn";      // พาธไฟล์ซีน "ตารางคะแนนสูงสุด"

	private Label _playerInfo;                   // ตัวแปรอ้างอิง Label ที่โชว์ชื่อ/วันที่สมัคร
	private Button _startBtn;                    // ตัวแปรอ้างอิงปุ่มเริ่ม
	private Button _highBtn;                     // ตัวแปรอ้างอิงปุ่ม High Score

	public override void _Ready()                // เรียกเมื่อโหนดพร้อม (ซีนถูกโหลดครบ)
	{
		// ----- หา Label แสดงชื่อ/วันที่สมัคร -----
		_playerInfo = GetNodeOrNull<Label>(PlayerInfoPath)               // พยายามดึงตาม path จาก Inspector ก่อน
					  ?? GetNodeOrNull<Label>("CanvasLayer/PlayerInfo"); // ถ้าไม่ได้ ใช้ path สำรองในซีน

		if (_playerInfo == null)                     // ถ้าไม่พบ Label
		{
			GD.PushError("PlayerInfo Label not found."); // แจ้งเตือนใน Output เพื่อดีบัก
		}
		else
		{
			ShowUserInfo();                           // ถ้าพบแล้ว แสดงข้อมูลผู้ใช้ทันที
		}

		// ----- หาและเชื่อมปุ่ม -----
		_startBtn = GetNodeOrNull<Button>(StartButtonPath)               // หา StartButton ตาม Inspector
					?? GetNodeOrNull<Button>("Sprite2D/StartButton");    // หรือ path สำรองในซีน
		_highBtn  = GetNodeOrNull<Button>(HighscoreButtonPath)           // หา HighscoreButton ตาม Inspector
					?? GetNodeOrNull<Button>("Sprite2D/HighscButton");   // หรือ path สำรองในซีน

		if (_startBtn != null) _startBtn.Pressed += OnStartPressed;      // ถ้าพบ: ผูกอีเวนต์กด → ไปเล่นเกม
		else GD.PushError("StartButton not found or not a Button.");     // ไม่พบ: แจ้งเตือน

		if (_highBtn != null) _highBtn.Pressed += OnHighscorePressed;    // ถ้าพบ: ผูกอีเวนต์กด → ไปหน้า High Score
		else GD.PushError("HighscButton not found or not a Button.");    // ไม่พบ: แจ้งเตือน
	}

	private void ShowUserInfo()                                           // แสดงชื่อผู้เล่น + วันที่สมัครบน Label
	{
		var pl = PlayerLogin.Instance;                                    // อ้างซิงเกิลตัน PlayerLogin (autoload)
		if (pl == null || _playerInfo == null) return;                    // ถ้าไม่มีระบบหรือไม่มี Label ให้จบ

		var u = pl.CurrentUser;                                           // ดึงผู้ใช้ปัจจุบัน
		if (u == null)                                                    // ถ้ายังไม่ได้ล็อกอินในรอบนี้
		{
			var list = pl.LoadPlayers();                                   // ลองโหลดผู้เล่นทั้งหมดจากไฟล์
			if (list.Count > 0) u = list[^1];                              // ถ้ามี ให้ใช้ตัวท้ายสุด (ล่าสุดโดยสมมติ)
		}
		if (u == null)                                                    // ถ้ายังว่างอยู่
		{
			_playerInfo.Text = "ไม่พบข้อมูลผู้เล่น";                      // แจ้งว่าไม่มีข้อมูล
			return;
		}

		// ===== Parse วันที่ให้ทนทาน แล้วแสดงผลเป็น ค.ศ. =====
		string dateText;                                                  // ตัวแปรเก็บสตริงวันที่ที่พร้อมโชว์
		try
		{
			DateTime dt;                                                  // ตัวแปรเก็บเวลาหลัง parse

			// พยายาม parse ตามรูปแบบที่พบบ่อยก่อน (ISO 8601 ฯลฯ)
			string[] isoFormats = {
				"yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'",
				"yyyy'-'MM'-'dd'T'HH':'mm':'ss'Z'",
				"yyyy'-'MM'-'dd'T'HH':'mm':'ss",
				"yyyy-MM-dd HH:mm:ss",
				"yyyy/MM/dd HH:mm:ss",
				"yyyy-MM-dd",
				"yyyy/MM/dd"
			};

			// TryParseExact: พยายามเทียบกับชุดรูปแบบข้างบน ใช้ InvariantCulture เพื่อไม่ขึ้นกับเลocaleเครื่อง
			if (!DateTime.TryParseExact(
					u.CreatedAt,
					isoFormats,
					CultureInfo.InvariantCulture,
					DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, // ถ้าขาดโซนเวลาให้ถือว่า UTC และแปลงเป็น UTC
					out dt))
			{
				// ถ้าไม่ตรงรูปแบบข้างบน ลอง Parse ธรรมดา (กันข้อมูลเก่า/แปลก)
				if (!DateTime.TryParse(
						u.CreatedAt,
						CultureInfo.InvariantCulture,
						DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
						out dt))
				{
					// ถ้ายังไม่ได้ ให้โชว์สตริงเดิม ไม่พยายามฟอร์แมต
					_playerInfo.Text = $"👤 {u.PlayerName}\n📅 {u.CreatedAt}";
					return;
				}
			}

			var local = dt.ToLocalTime();                                  // แปลงเวลาเป็นตามโซนเครื่องผู้เล่น
			// ใช้ InvariantCulture + รูปแบบ dd/MM/yyyy (MM = เดือน) → แน่ใจว่าแสดงปีค.ศ. ไม่งอแงกับ locale
			dateText = local.ToString("dd'/'MM'/'yyyy", CultureInfo.InvariantCulture);
		}
		catch
		{
			// ถ้าเกิดข้อผิดพลาดใด ๆ (เช่นสตริงเพี้ยน) แสดงข้อความเดิมจากข้อมูล
			dateText = u.CreatedAt;
		}

		_playerInfo.Text = $"👤 {u.PlayerName}\n📅 {dateText}";           // สร้างสตริงแสดงชื่อ + วันที่
		_playerInfo.AddThemeColorOverride("font_color", new Color(1, 1, 1)); // ปรับสีตัวอักษรเป็นขาว
		_playerInfo.AddThemeFontSizeOverride("font_size", 22);            // ปรับขนาดฟอนต์
		_playerInfo.MouseFilter = Control.MouseFilterEnum.Ignore;         // ไม่กินอีเวนต์เมาส์ (ผ่านไปข้างล่าง)
	}

	private void OnStartPressed()                                          // callback เมื่อกดปุ่มเริ่มเกม
	{
		if (ResourceLoader.Exists(PLAY_SCENE))                             // ตรวจว่ามีไฟล์ซีนจริง
			GetTree().ChangeSceneToFile(PLAY_SCENE);                       // เปลี่ยนซีนไปยังฉากเล่นเกม
		else
			GD.PushError($"Play scene not found: {PLAY_SCENE}");           // ไม่เจอไฟล์ → แจ้งเตือนใน Output
	}

	private void OnHighscorePressed()                                      // callback เมื่อกดปุ่ม High Score
	{
		if (ResourceLoader.Exists(HIGHSCORE_SCENE))                        // ตรวจว่ามีไฟล์ซีนจริง
			GetTree().ChangeSceneToFile(HIGHSCORE_SCENE);                  // เปลี่ยนซีนไปยังฉาก High Score
		else
			GD.PushError($"Highscore scene not found: {HIGHSCORE_SCENE}"); // ไม่เจอไฟล์ → แจ้งเตือนใน Output
	}
}
