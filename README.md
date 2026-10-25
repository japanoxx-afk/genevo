# genevo

Red Alert 3 / Generals Evolution 리플레이용 Windows 런처와 관전 오버레이입니다. v0.2.0부터 검증된 Steam 1.12 실행 파일에서 실제 플레이어의 자금·전력·진영·색을 직접 읽습니다. 분당 수입과 원본 HUD 숨기기는 아직 지원하지 않습니다.

## 실행

GitHub Releases의 `genevo-win-x64.zip`을 풀고 `GenevoLauncher.exe`를 실행합니다. 소스 폴더에서는 `scripts/build.ps1`로 빌드한 후 `Genevo.cmd`를 실행할 수 있습니다. .NET Framework 4.x가 있는 Windows 10/11 x64를 대상으로 합니다.

1. 런처에서 게임 폴더, GenEvo 폴더, `.RA3Replay` 파일을 선택합니다.
2. 게임이 독점 전체 화면으로 실행되고 있다면 직접 종료하고 **창 모드로 리플레이 실행**을 누릅니다. 이 버튼은 `RA3.exe -win -modConfig <skudef> -replayGame <replay>`로 실행하며, 실행 중인 게임을 자동 종료하지 않습니다. GenEvo 0.33 문서에서 요구하는 Tacitus 설치를 먼저 완료해야 합니다. 공식 GenEvo 런처 버튼도 유지됩니다.
3. 게임을 창 모드 또는 테두리 없는 창 모드로 실행합니다. 권장 최소 해상도는 1280×720입니다. 독점 전체 화면 위에는 외부 HUD가 표시되지 않을 수 있습니다.
4. 게임 안에서 **F7**을 누르고 원본 미니맵을 드래그합니다. 런처의 **미니맵 영역 지정** 버튼도 게임 복원을 시도한 뒤 선택 화면을 표시합니다. ESC로 취소합니다. 영역은 게임 클라이언트에 대한 비율로 저장됩니다.
5. 리플레이 파일을 선택해 두면 게임으로 돌아갈 때 HUD가 자동으로 켜집니다. **F8**로 HUD를 전환합니다. 런처를 닫으면 HUD도 종료됩니다. 파일 선택만으로 실제 재생 파일과 일치하는지는 확인할 수 없습니다.

미니맵은 원본 화면을 복제합니다. 원본 게임 HUD와 미니맵은 그대로 남으며, 지정한 영역이 가려지면 복제본도 가려집니다. 전체 화면 독점 모드, 원본 HUD 위치 변경, 미니맵 클릭 조작은 지원하지 않습니다. 모드 BIG 파일과 게임 실행 파일을 수정하지 않습니다. 기존 모드 설치를 자동 교체하지 않습니다.

## 구현 상태

| 기능 | 상태 |
| --- | --- |
| 매 실행 GitHub 버전 확인 | 구현, 공개 Releases 사용 |
| 새 버전 자동 다운로드·검증·전환 | GitHub v0.1.1 패키지로 설치·전환 검증 완료 |
| 오프라인/릴리스 미등록 시 기존 버전 실행 | 구현 |
| 공식 GenEvo 런처 연결 | 구현. 창 모드 명령으로 실제 리플레이 실행 확인 |
| 리플레이 맵·플레이어 이름 | BIG 내 압축 맵의 표시 이름 추출 및 실제 리플레이 파싱 확인 |
| 왼쪽 아래 미니맵 | 지정 영역 화면 복제, 실제 게임 화면 검증 필요 |
| 하단 플레이어 HUD | 구현, 렌더링 검증 |
| 진영 이름·플레이어 색 | 실제 플레이어의 진영 식별자와 ARGB 색 읽기 |
| 실시간 자금·전력 | 지원 빌드에서 200ms마다 직접 읽기. 실제 값 변화 확인 |
| 분당 수입 | 미지원. 보유 금액 변화는 지출도 포함하므로 수입으로 추정하지 않음 |
| 마우스 고정 | 게임 활성 중에만 적용. F6 전환, Alt/다른 창/종료 시 해제 |

내장 수집기는 게임 메모리를 외부에서 읽기만 하며 코드를 주입하거나 게임에 쓰지 않습니다. 실행 파일 SHA-256, 코드 패턴, ReplayObserver 모드, 플레이어 이름·인덱스, 자원 객체 소유 인덱스와 값 범위를 검증합니다. 검증 실패 시 값을 숨깁니다. 현재 지원 SHA-256은 `29AA6B66FC298188FA42DF7218A1DEA7E181A946A54D0D8841000906F9F4D98E`입니다. 다른 실행 파일 빌드는 추가 프로필 검증이 필요합니다. 실제 재생 파일명까지 확인하지는 못하므로 선택한 파일과 재생 파일을 맞춰주세요.

## 자동 업데이트

EXE 실행 → 동봉된 `scripts/bootstrap.ps1` → 최신 정식 GitHub Release 확인 → 새 버전 ZIP과 SHA-256 파일 다운로드 → 압축 항목 및 VERSION 검증 → `%LOCALAPPDATA%/GenevoObserver/versions/<version>`에 설치 → 버전 포인터 전환 → 런처 실행 순서입니다. 동일 실행에서 버전 확인은 한 번 수행됩니다.

네트워크 장애·손상 패키지는 로그를 남기고 기존 버전을 실행합니다. 설정은 `%LOCALAPPDATA%/GenevoObserver/settings.json`, 업데이트 로그는 `update.log`에 있습니다. SHA-256은 전송 손상을 확인하며 코드 서명은 아닙니다. 저장소가 비공개이면 인증 없는 자동 업데이트는 동작하지 않습니다.

새 버전 배포: `VERSION`, 어셈블리 버전 및 UI 버전을 변경하고 `RELEASE_NOTES.md`를 갱신한 뒤 `v<version>` 태그를 푸시합니다. GitHub Actions가 빌드·검증 후 ZIP과 SHA-256을 Releases에 게시합니다. 게임·모드 파일은 배포하지 않습니다.

## 데이터 연결 규약

내장 수집기는 HUD에 직접 전달하므로 JSON 파일이 필요하지 않습니다. 외부 수집기용 `%LOCALAPPDATA%/GenevoObserver/telemetry.json` 규약도 유지합니다. 외부 파일은 `CapturedUtc`가 3초 이상 오래되거나 `Replay`가 선택 파일명과 다르면 무시합니다. 슬롯은 헤더 내 플레이어 순서이며 관전자/해설자는 제외합니다.

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

이 예시는 외부 연결 규약 설명용입니다. 내장 수집기는 `Income`을 채우지 않으며 ReplayObserver 모드를 확인한 경우에만 자원을 읽습니다. 외부 수집기의 게임/리플레이 일치 검증은 해당 수집기가 담당해야 합니다.

## 개발·검증

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build.ps1
$p = Start-Process dist/GenevoLauncher.exe -ArgumentList '--self-test' -Wait -PassThru
$p.ExitCode
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/package.ps1
```

자가 검증은 헤더 파싱, 잘못된 파일 거부, 로컬 리플레이가 있을 때 실제 파일 파싱, HUD 이미지 생성을 확인합니다. v0.1.2에서는 테스트 창을 사용해 작은 보조 창 제외, 최소화 복원, HUD 표시·좌표·포커스 유지 및 최소화 시 숨김도 확인합니다. 활성 게임 조건은 테스트에서 주입하며 실제 Direct3D 화면의 합성까지 검증하지는 않습니다. 출력은 `%LOCALAPPDATA%/GenevoObserver/test-result.txt`와 `hud-preview.png`입니다.

2026-10-26 로컬 검증: 초기 버전으로 표시한 별도 테스트 폴더에서 부트스트랩을 실행해 GitHub Releases의 v0.1.1 다운로드, SHA-256 확인, 버전 폴더 설치, current.txt 전환 및 설치된 EXE 실행을 확인했습니다. 릴리스가 없을 때 기존 EXE로 돌아오는 동작도 확인했습니다. 실제 게임 리플레이 위의 표시·캡처와 GenEvo 실행 호환성은 아직 검증하지 않았습니다.

참고: [RA3ReplayParser 헤더 형식](https://github.com/NeWive/RA3ReplayParser), [RA3 리플레이 컨테이너 문서](https://github.com/Abgetrennter/RA3-Replay-Format/blob/master/docs/01-container.md). 외부 소스 코드를 복사하지 않았습니다.

v0.2.0 로컬 검증: 실제 리플레이에서 `[GenEvo-Dev] Cold Drops`, `GLA 독소`, `미국`을 HUD 화면에서 확인했습니다. 두 차례 실제 자금 샘플 `8035/8962`와 `5420/4586`, 전력 변화 `0/0`→`50/200`을 확인했습니다. 게임 활성 중 커서 제한 영역 일치와 런처 전환 시 고정 해제도 확인했습니다. 자금은 보유 금액이므로 생산·건설 지출 시 내려갑니다. `GenevoLauncher.exe --diagnose-live` 진단 결과는 로컬 `live-diagnostic.json`에만 기록합니다.

마우스는 게임 활성 중에만 창 안으로 고정됩니다. **F6** 또는 런처 체크박스로 전환합니다. Alt 키, 다른 창 전환, 런처 종료, 미니맵 영역 지정 중에는 해제됩니다.
