using Godot;
using System;
using System.Globalization;

public partial class StartGame : Node2D
{
	[Export] private NodePath PlayerInfoPath;

	// ปุ่ม (ตั้งผ่าน Inspector ได้ หรือปล่อยให้หาโดยชื่อในซีน)
	[Export] private NodePath StartButtonPath;
	[Export] private NodePath HighscoreButtonPath;

	// ← แก้ให้เป็น path ของโปรเจกต์คุณจริง ๆ
	private const string PLAY_SCENE      = "res://scenecheckpoint/checkpoint.tscn";
	private const string HIGHSCORE_SCENE = "res://SceneHighSc/HighScore.tscn";

	private Label _playerInfo;
	private Button _startBtn;
	private Button _highBtn;

	public override void _Ready()
	{
		// ----- หา Label แสดงชื่อ/วันที่สมัคร -----
		_playerInfo = GetNodeOrNull<Label>(PlayerInfoPath)
					  ?? GetNodeOrNull<Label>("CanvasLayer/PlayerInfo");

		if (_playerInfo == null)
		{
			GD.PushError("PlayerInfo Label not found.");
		}
		else
		{
			ShowUserInfo();
		}

		// ----- หาและเชื่อมปุ่ม -----
		_startBtn = GetNodeOrNull<Button>(StartButtonPath)
					?? GetNodeOrNull<Button>("Sprite2D/StartButton");
		_highBtn  = GetNodeOrNull<Button>(HighscoreButtonPath)
					?? GetNodeOrNull<Button>("Sprite2D/HighscButton");

		if (_startBtn != null) _startBtn.Pressed += OnStartPressed;
		else GD.PushError("StartButton not found or not a Button.");

		if (_highBtn != null) _highBtn.Pressed += OnHighscorePressed;
		else GD.PushError("HighscButton not found or not a Button.");
	}

	private void ShowUserInfo()
	{
		var pl = PlayerLogin.Instance;
		if (pl == null || _playerInfo == null) return;

		var u = pl.CurrentUser;
		if (u == null)
		{
			var list = pl.LoadPlayers();
			if (list.Count > 0) u = list[^1];
		}
		if (u == null)
		{
			_playerInfo.Text = "ไม่พบข้อมูลผู้เล่น";
			return;
		}

		string dateText;
		try
		{
			var utc   = DateTime.Parse(u.CreatedAt, null, DateTimeStyles.AdjustToUniversal);
			var local = utc.ToLocalTime();
			dateText  = local.ToString("dd/MM/yyyy", new CultureInfo("th-TH"));
		}
		catch { dateText = u.CreatedAt; }

		_playerInfo.Text = $"👤 {u.PlayerName}\n📅 {dateText}";
		_playerInfo.AddThemeColorOverride("font_color", new Color(1, 1, 1));
		_playerInfo.AddThemeFontSizeOverride("font_size", 22);
		_playerInfo.MouseFilter = Control.MouseFilterEnum.Ignore;
	}

	private void OnStartPressed()
	{
		if (ResourceLoader.Exists(PLAY_SCENE))
			GetTree().ChangeSceneToFile(PLAY_SCENE);
		else
			GD.PushError($"Play scene not found: {PLAY_SCENE}");
	}

	private void OnHighscorePressed()
	{
		if (ResourceLoader.Exists(HIGHSCORE_SCENE))
			GetTree().ChangeSceneToFile(HIGHSCORE_SCENE);
		else
			GD.PushError($"Highscore scene not found: {HIGHSCORE_SCENE}");
	}
}
