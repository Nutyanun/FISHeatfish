using Godot;

public partial class Hud : CanvasLayer
{
	[Export] private Label ScoreLabel { get; set; }
	[Export] private Label LivesLabel { get; set; }
	[Export] private Label LevelLabel { get; set; }

	private Control _overlay;   // %GameOverLabel
	private Label _title;
	private Label _hint;
	private Button _retry;
	private Button _quit;

	public override void _Ready()
	{
		ProcessMode = Node.ProcessModeEnum.Always;

		ScoreLabel ??= GetNodeOrNull<Label>("%ScoreLabel") ?? GetNodeOrNull<Label>("ScoreLabel");
		LivesLabel ??= GetNodeOrNull<Label>("%LivesLabel") ?? GetNodeOrNull<Label>("LivesLabel");
		LevelLabel ??= GetNodeOrNull<Label>("%LevelLabel") ?? GetNodeOrNull<Label>("LevelLabel");

		_overlay = GetNodeOrNull<Control>("%GameOverLabel") ?? GetNodeOrNull<Control>("GameOverLabel");
		if (_overlay != null)
		{
			_title = _overlay.GetNodeOrNull<Label>("%Title") ?? _overlay.GetNodeOrNull<Label>("Center/rood/Title");
			_hint  = _overlay.GetNodeOrNull<Label>("%Hint")  ?? _overlay.GetNodeOrNull<Label>("Center/rood/Hint");
			_retry = _overlay.GetNodeOrNull<Button>("%Retry") ?? _overlay.GetNodeOrNull<Button>("Center/rood/Buttons/Retry");
			_quit  = _overlay.GetNodeOrNull<Button>("%Quit")  ?? _overlay.GetNodeOrNull<Button>("Center/rood/Buttons/Quit");

			_overlay.ProcessMode = Node.ProcessModeEnum.Always;
			_overlay.ZIndex = 1000;
			_overlay.Visible = false;  // ซ่อนตั้งแต่เริ่ม
			_overlay.MoveToFront();
		}

		if (_retry != null) _retry.Pressed += OnRetryPressed;
		if (_quit  != null) _quit.Pressed  += OnQuitPressed;

		GetTree().Paused = false;   // กันค้าง pause จากรอบก่อน
		HideOverlay();
		GD.Print("[HUD] ready");
	}

	// ====== มีแค่ 3 เมธอดนี้สำหรับอัปเดต ======
	public void UpdateScore(int cur, int target)
	{
		if (ScoreLabel != null) ScoreLabel.Text = $"Score : {cur} / {target}";
	}
	public void UpdateLives(int lives)
	{
		if (LivesLabel != null) LivesLabel.Text = $"Lives : {lives}";
	}
	public void UpdateLevel(int level)
	{
		if (LevelLabel != null) LevelLabel.Text = $"Level : {level}";
	}

	// ====== แสดง/ซ่อน Game Over ======
	public void ShowGameOver()
	{
		if (_overlay == null) return;
		if (_title != null) _title.Text = "GAME OVER";
		if (_hint  != null) _hint.Text  = "Press Enter to retry";
		_overlay.Visible = true;
		_overlay.MoveToFront();
		GetTree().Paused = true;
		GD.Print("[HUD] show game over");
	}
	public void HideOverlay()
	{
		if (_overlay != null) _overlay.Visible = false;
	}

	private void OnRetryPressed()
	{
		GetTree().Paused = false;
		HideOverlay();
		GetTree().ReloadCurrentScene();
	}
	private void OnQuitPressed() => GetTree().Quit();

	public override void _UnhandledInput(InputEvent e)
{
	// รับอินพุตเฉพาะตอนเกมโอเวอร์เท่านั้น
	if (_overlay == null || !_overlay.Visible) return;

	if (e.IsActionPressed("ui_accept") || e.IsActionPressed("restart"))
		OnRetryPressed();

	if (e.IsActionPressed("ui_cancel") || e.IsActionPressed("quit"))
		OnQuitPressed();
}

}
