# CPU の女の子の声を、Windows の日本語の音声合成で WAV に書き出す(1回だけ実行すればよい)。
# セリフは Assets/_Project/Common/Resources/Voice/lines.txt(1行に1つ)。書き出し先は同じフォルダの voice_00.wav, voice_01.wav ...
# 使い方:エクスプローラーでこのファイルを右クリック →「PowerShell で実行」、または
#   powershell -ExecutionPolicy Bypass -File Tools\make_voices.ps1
# 声の高さと速さは下の $pitch と $rate で変えられる。
param(
    [string]$pitch = "+25%",
    [string]$rate = "1.05"
)

Add-Type -AssemblyName System.Speech
$root = Split-Path -Parent $PSScriptRoot
$folder = Join-Path $root "Assets\_Project\Common\Resources\Voice"
$lines = Get-Content -Path (Join-Path $folder "lines.txt") -Encoding UTF8 | Where-Object { $_.Trim() -ne "" }

$synth = New-Object System.Speech.Synthesis.SpeechSynthesizer
# 日本語の女性の声を優先して選ぶ
$voice = $synth.GetInstalledVoices() | Where-Object { $_.Enabled -and $_.VoiceInfo.Culture.Name -eq "ja-JP" } |
    Sort-Object { if ($_.VoiceInfo.Gender -eq "Female") { 0 } else { 1 } } | Select-Object -First 1
if ($voice -eq $null) {
    Write-Host "日本語の音声合成の声が見つかりません。Windows の設定 > 時刻と言語 > 音声認識 で日本語の音声を追加してください。"
    exit 1
}
Write-Host ("声: " + $voice.VoiceInfo.Name)

for ($i = 0; $i -lt $lines.Count; $i++) {
    $text = $lines[$i].Trim()
    $out = Join-Path $folder ("voice_{0:D2}.wav" -f $i)
    $ssml = "<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='ja-JP'>" +
            "<voice name='" + $voice.VoiceInfo.Name + "'><prosody pitch='" + $pitch + "' rate='" + $rate + "'>" +
            [System.Security.SecurityElement]::Escape($text) + "</prosody></voice></speak>"
    $synth.SetOutputToWaveFile($out)
    $synth.SpeakSsml($ssml)
    $synth.SetOutputToNull()
    Write-Host ("{0:D2} {1}" -f $i, $text)
}

$synth.Dispose()
Write-Host "できました。Unity に戻ると取り込まれます。"
