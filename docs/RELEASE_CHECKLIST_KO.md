# GitHub 공개 전 체크리스트

- [ ] `python tools/audit_public_repo.py`가 PASS
- [ ] 실제 게임 파일/게임 DLL/게임 에셋이 Git 추적 대상에 없음
- [ ] 일본어 원문 ↔ 한국어 번역 매핑 및 자동 캐시가 없음
- [ ] 개인 절대 경로, API 키, 토큰, 로그, 덤프가 없음
- [ ] RlyehTextFix 소스와 빌드 도구의 MIT 범위가 명확함
- [ ] XUnity 패치가 v5.6.1 / `7f1f3b...` 기준임
- [ ] Il2CppInterop 패치가 v1.5.3 / `dbda1cb...` 기준임
- [ ] 수정 Il2CppInterop 바이너리 배포 시 `LGPL-3.0-only` 대응 소스, 변경 고지, 라이선스 전문 제공 조건을 충족함
- [ ] 폰트 번들 배포 시 OFL 1.1 및 고지를 함께 제공
- [ ] Release asset의 SHA-256을 다시 계산하고 게시
- [ ] README의 verified environment와 실제 release binary가 일치함
- [ ] 현재 GitHub checkout에서 RlyehTextFix Release 빌드가 성공함
