# genevo

Red Alert 3 / Generals Evolution 리플레이용 Windows 런처와 관전 오버레이입니다. **현재 버전은 초기 개발 버전이며, 요청한 실시간 자원 HUD는 아직 완성되지 않았습니다.**

## 실행

GitHub Releases의 `genevo-win-x64.zip`을 풀고 `GenevoLauncher.exe`를 실행합니다. 소스 폴더에서는 `scripts/build.ps1`로 빌드한 후 `Genevo.cmd`를 실행할 수 있습니다. .NET Framework 4.x가 있는 Windows 10/11 x64를 대상으로 합니다.

1. 런처에서 게임 폴더, GenEvo 폴더, `.RA3Replay` 파일을 선택합니다.
2. **GenEvo 공식 런처 실행**을 눌러 모드를 시작하고 게임 안에서 해당 리플레이를 재생합니다. GenEvo 0.33 문서에서 요구하는 Tacitus 설치를 먼저 완료해야 합니다.
3. 게임을 창 모드 또는 테두리 없는 창 모드로 실행합니다. 권장 최소 해상도는 1280×720입니다.
4. **미니맵 영역 지정**에서 원본 미니맵을 드래그합니다. 영역은 게임 클라이언트에 대한 비율로 저장됩니다.
5. **관전 HUD 켜기**를 누르고 게임으로 돌아갑니다. F8로 HUD를 전환합니다. 런처를 닫으면 HUD도 종료됩니다.

미니맵은 원본 화면을 복제합니다. 원본 게임 HUD와 미니맵은 그대로 남으며, 지정한 영역이 가려지면 복제본도 가려집니다. 전체 화면 독점 모드, 원본 HUD 위치 변경, 미니맵 클릭 조작은 지원하지 않습니다. 모드 BIG 파일과 게임 실행 파일을 수정하지 않습니다. 기존 모드 설치를 자동 교체하지 않습니다.

## 구현 상태

| 기능 | 상태 |
| --- | --- |
| 매 실행 GitHub 버전 확인 | 구현, 공개 Releases 사용 |
| 새 버전 자동 다운로드·검증·전환 | GitHub v0.1.1 패키지로 설치·전환 검증 완료 |
| 오프라인/릴리스 미등록 시 기존 버전 실행 | 구현 |
| 공식 GenEvo 런처 연결 | 구현, 실제 게임 실행은 수동 확인 필요 |
| 리플레이 맵·플레이어 이름 | 실제 로컬 리플레이 파싱 검증 |
| 왼쪽 아래 미니맵 | 지정 영역 화면 복제, 실제 게임 화면 검증 필요 |
| 하단 플레이어 HUD | 구현, 렌더링 검증 |
| 진영 이름·플레이어 색 | 미확인. 진영은 원시 ID, 색은 슬롯별 표시색 |
| 실시간 자원·수입·전력 | 수집기 미구현. 값은 — 표시 |

리플레이는 명령 기록이며 실시간 경제 상태를 그대로 제공하지 않습니다. 게임 메모리/관전자 UI 바인딩 또는 모드 계측에서 데이터를 얻는 어댑터가 추가로 필요합니다. 확인되지 않은 메모리 주소를 읽거나 임의 값을 실제 값처럼 표시하지 않습니다.

## 자동 업데이트

EXE 실행 → 동봉된 `scripts/bootstrap.ps1` → 최신 정식 GitHub Release 확인 → 새 버전 ZIP과 SHA-256 파일 다운로드 → 압축 항목 및 VERSION 검증 → `%LOCALAPPDATA%/GenevoObserver/versions/<version>`에 설치 → 버전 포인터 전환 → 런처 실행 순서입니다. 동일 실행에서 버전 확인은 한 번 수행됩니다.

네트워크 장애·손상 패키지는 로그를 남기고 기존 버전을 실행합니다. 설정은 `%LOCALAPPDATA%/GenevoObserver/settings.json`, 업데이트 로그는 `update.log`에 있습니다. SHA-256은 전송 손상을 확인하며 코드 서명은 아닙니다. 저장소가 비공개이면 인증 없는 자동 업데이트는 동작하지 않습니다.

새 버전 배포: `VERSION`, 어셈블리 버전 및 UI 버전을 변경하고 `RELEASE_NOTES.md`를 갱신한 뒤 `v<version>` 태그를 푸시합니다. GitHub Actions가 빌드·검증 후 ZIP과 SHA-256을 Releases에 게시합니다. 게임·모드 파일은 배포하지 않습니다.

## 데이터 연결 규약

추가 수집기는 `%LOCALAPPDATA%/GenevoObserver/telemetry.json`을 임시 파일에서 원자적으로 교체해야 합니다. 현재 수집기는 포함되지 않습니다. `CapturedUtc`가 3초 이상 오래되거나 `Replay`가 선택 파일명과 다르면 HUD는 값을 숨깁니다. 슬롯은 헤더 내 플레이어 순서이며 관전자/해설자는 제외합니다.

```json
{
  "Replay": "example.RA3Replay",
  "CapturedUtc": "2026-10-25T16:00:00Z",
  "GameTime": "07:47",
  "Players": [
    { "Slot": 0, "Faction": "검증된 진영명", "Credits": 2486, "Income": null, "PowerUsed": 147, "PowerTotal": 190 }
  ]
}
```

이 예시는 연결 규약 설명용입니다. `Income`의 단위는 수집기 구현 시 확정해야 합니다. 게임 프로세스/선택 리플레이의 일치 검증은 수집기가 담당해야 하며, 현재 런처는 실제 재생 중인 파일을 확인하지 못합니다. 일반 대전에서도 수동으로 HUD를 켤 수 있으므로 리플레이 관전에만 사용하세요.

## 개발·검증

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build.ps1
$p = Start-Process dist/GenevoLauncher.exe -ArgumentList '--self-test' -Wait -PassThru
$p.ExitCode
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/package.ps1
```

자가 검증은 헤더 파싱, 잘못된 파일 거부, 로컬 리플레이가 있을 때 실제 파일 파싱, HUD 이미지 생성을 확인합니다. 출력은 `%LOCALAPPDATA%/GenevoObserver/test-result.txt`와 `hud-preview.png`입니다.

2026-10-26 로컬 검증: 초기 버전으로 표시한 별도 테스트 폴더에서 부트스트랩을 실행해 GitHub Releases의 v0.1.1 다운로드, SHA-256 확인, 버전 폴더 설치, current.txt 전환 및 설치된 EXE 실행을 확인했습니다. 릴리스가 없을 때 기존 EXE로 돌아오는 동작도 확인했습니다. 실제 게임 리플레이 위의 표시·캡처와 GenEvo 실행 호환성은 아직 검증하지 않았습니다.

참고: [RA3ReplayParser 헤더 형식](https://github.com/NeWive/RA3ReplayParser), [RA3 리플레이 컨테이너 문서](https://github.com/Abgetrennter/RA3-Replay-Format/blob/master/docs/01-container.md). 외부 소스 코드를 복사하지 않았습니다.
