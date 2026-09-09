# 기술 개요

RlyehTextFix는 DMM Windows판 `rlyehshoujotaix_cl`의 Unity 6 IL2CPP 환경에서
XUnity.AutoTranslator를 사용할 때 확인된 표시/타이핑 문제를 보정하기 위한
BepInEx 플러그인입니다.

핵심 기능은 다음과 같습니다.

- 번역된 한국어 문장 길이에 맞춘 스토리 타이핑 진행률 보정
- TextMesh Pro 오버레이를 이용한 스토리 폰트 통일
- 컷씬 전환 후 첫 문장 재감지
- 보이는 오버레이에서 UTAGE 제어 태그를 숨기되 원래 연출은 유지
- XUnity 5.6.1의 `RichTextParser.Parse(string,int)`를 외부 Harmony 패치하여
  표시 전용 TMP 태그가 번역 문맥을 조각내지 않도록 보정

번역 문맥에서 기본적으로 무시할 수 있는 표시 태그:

`cspace`, `size`, `color`, `indent`, `line-indent`, `line-height`, `scale`,
`voffset`, `pos`, `width`, `margin`, `margin-left`, `margin-right` 및 TMP 축약
색상 태그.

안전상 보존되는 태그:

`interval`, `speed`, `sound`, `sprite`, `br`, `link`, `page`, `nobr`.

이 저장소는 번역문이나 게임 원문 데이터셋을 배포하지 않습니다. 실제 게임
문자열을 사용한 과거 테스트 케이스는 공개 버전에서 제거하거나 일반화된 샘플로
대체합니다.
