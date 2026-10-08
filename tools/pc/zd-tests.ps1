# PlayMode-тесты: без параметра — запуск и ожидание результата; -job <id> — только прочитать задание.
param([string]$job = "", [string]$filter = "")
# -filter: имя теста/класса или список через запятую (ZeldaDaughter.Tests.GameSessionTests,ZeldaDaughter.Tests.W0Tests)
$env:UMCP_PROJECT = "ZeldaDaughter"
$uv = "C:\Users\paper\.local\bin\uv.exe"
$u = "C:\dev\zelda-tools\umcp.py"
if ($job -eq "") {
  $args1 = '{\"mode\":\"PlayMode\",\"assembly_names\":[\"ZeldaDaughter.Tests.PlayMode\"]}'
  if ($filter -ne "") { $names = ($filter.Split(",") | ForEach-Object { '\"' + $_.Trim() + '\"' }) -join ","; $args1 = '{\"mode\":\"PlayMode\",\"assembly_names\":[\"ZeldaDaughter.Tests.PlayMode\"],\"test_names\":[' + $names + ']}' }
  & $uv run --no-project python $u call run_tests $args1 2>&1 | Out-String
} else {
  & $uv run --no-project python $u call get_test_job ('{\"job_id\":\"' + $job + '\",\"include_details\":true}') 2>&1 | Out-String
}
