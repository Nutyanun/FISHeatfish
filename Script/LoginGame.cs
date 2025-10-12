using Godot;
using System;
using System.Collections.Generic;

public partial class LoginGame : Control
{
	// ตั้งผ่าน Inspector ได้ (เผื่อ path ในฉากเปลี่ยน)
	[Export] private NodePath NameInputPath;
	[Export] private NodePath PasswordInputPath;
	[Export] private NodePath ErrorLabelPath;
	[Export] private NodePath SubmitButtonPath;

	private LineEdit _nameInput;
	private LineEdit _passwordInput;
	private Label _errorLabel;
	private Button _submitButton;

	// อักขระพิเศษที่อนุญาตในรหัสผ่าน
	private const string AllowedSpecials = @"!@#$%^&*()-_=+[]{};:'"",.<>/?\|`~";

	public override void _Ready()
	{
		_nameInput     = GetNodeOrNull<LineEdit>(NameInputPath)
					  ?? GetNodeOrNull<LineEdit>("CenterContainer/VBoxContainer/NameInput");

		_passwordInput = GetNodeOrNull<LineEdit>(PasswordInputPath)
					  ?? GetNodeOrNull<LineEdit>("CenterContainer/VBoxContainer/PasswordInput");

		_errorLabel    = GetNodeOrNull<Label>(ErrorLabelPath)
					  ?? GetNodeOrNull<Label>("CenterContainer/VBoxContainer/ErrorLabel");

		_submitButton  = GetNodeOrNull<Button>(SubmitButtonPath)
					  ?? GetNodeOrNull<Button>("CenterContainer/VBoxContainer/SubmitButton");

		if (_nameInput == null)     GD.PushError("NameInput not found.");
		if (_passwordInput == null) GD.PushError("PasswordInput not found.");
		if (_errorLabel == null)    GD.PushError("ErrorLabel not found.");
		if (_submitButton == null)  GD.PushError("SubmitButton not found.");

		if (_errorLabel != null) _errorLabel.Visible = false;
		if (_submitButton != null) _submitButton.Pressed += OnSubmit;

		if (_passwordInput != null) _passwordInput.Secret = true; // ซ่อนรหัส
	}

	// -------------------- MAIN FLOW --------------------
	private void OnSubmit()
	{
		if (_nameInput == null || _passwordInput == null || _errorLabel == null)
		{
			GD.PushError("UI nodes missing, cannot submit.");
			return;
		}

		// 1) ตรวจชื่อ
		string name = (_nameInput.Text ?? "").Trim();
		if (name.Length == 0)
		{
			ShowError("กรุณากรอกชื่ออย่างน้อย 1 ตัวอักษร");
			return;
		}
		string badName = GetInvalidNameChars(name);
		if (!string.IsNullOrEmpty(badName))
		{
			ShowError($"ไม่สามารถใช้ชื่อนี้ได้ เพราะมีตัวอักษรพิเศษ: {badName}");
			return;
		}

		// 2) ตรวจรหัสผ่าน
		string password = (_passwordInput.Text ?? "").Trim();
		string pwdErr = ValidatePassword(password);
		if (pwdErr != null)
		{
			ShowError(pwdErr);
			return;
		}

		// 3) อ้างอิงระบบผู้เล่น (Autoload: PlayerLogin)
		var saver = PlayerLogin.Instance ?? GetNodeOrNull<PlayerLogin>("/root/PlayerLogin");
		if (saver == null)
		{
			ShowError("ระบบ PlayerLogin (Autoload) ไม่ได้เปิดใช้งาน");
			GD.PushError("Missing /root/PlayerLogin. Add Autoload.");
			return;
		}

		// 4) ลองล็อกอินผู้ใช้เดิมก่อน
		if (saver.LoginExisting(name, password))
		{
			HideError();
			GD.Print($"✅ Login OK: {saver.CurrentUser?.PlayerName}");
			GoNext();
			return;
		}

		// 5) ถ้าไม่พบ ก็สมัครใหม่
		if (!saver.SavePlayer(name, password))
		{
			ShowError("ชื่อนี้ถูกใช้แล้ว กรุณาลองชื่ออื่น");
			return;
		}

		// SavePlayer() ของคุณจะตั้ง CurrentUser ให้แล้ว
		// กันพลาด: ถ้ายังไม่มี CurrentUser ให้ตั้งเพิ่ม
		if (saver.CurrentUser == null)
		{
			saver.CurrentUser = new PlayerLogin.SaveData
			{
				PlayerName = name,
				Password   = password,
				CreatedAt  = DateTime.UtcNow.ToString("o"),
			};
		}

		HideError();
		GD.Print($"🎉 Registered & Login: {saver.CurrentUser?.PlayerName}");
		GoNext();
	}

	// -------------------- Scene Transition --------------------
	private void GoNext()
	{
		HideError();
		GD.Print($"Login success → {(_nameInput?.Text ?? "")}");
		const string nextScene = "res://SceneStartandHigh/StartGame.tscn";
		if (ResourceLoader.Exists(nextScene))
			GetTree().ChangeSceneToFile(nextScene);
		else
			ShowError("ไม่พบซีนถัดไป: " + nextScene);
	}

	// -------------------- Helpers --------------------
	// อนุญาต A–Z a–z 0–9 และอักษรไทย
	private string GetInvalidNameChars(string s)
	{
		var list = new List<char>();
		foreach (char c in s)
		{
			if (!(char.IsLetterOrDigit(c) || (c >= 0x0E00 && c <= 0x0E7F)))
				if (!list.Contains(c)) list.Add(c);
		}
		return string.Join(", ", list);
	}

	// รหัสผ่านต้อง ≥ 6 ตัว และมีเฉพาะ a-z A-Z 0-9 หรืออักขระใน AllowedSpecials
	private string ValidatePassword(string pwd)
	{
		if (string.IsNullOrEmpty(pwd) || pwd.Length < 6)
			return "รหัสผ่านต้องมีอย่างน้อย 6 ตัว";

		var invalids = new List<char>();
		foreach (char c in pwd)
		{
			if (char.IsLetterOrDigit(c)) continue;
			if (AllowedSpecials.IndexOf(c) >= 0) continue;
			if (!invalids.Contains(c)) invalids.Add(c);
		}
		if (invalids.Count > 0)
			return $"รหัสผ่านมีอักขระที่ไม่อนุญาต: {string.Join(", ", invalids)}";

		return null; // ผ่าน
	}

	private void ShowError(string msg)
	{
		_errorLabel.Text = msg;
		_errorLabel.Visible = true;
		_errorLabel.Modulate = new Color(1, 0, 0);
	}

	private void HideError()
	{
		_errorLabel.Text = "";
		_errorLabel.Visible = false;
	}
}
