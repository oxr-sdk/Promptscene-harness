#!/usr/bin/env bash
# build-index.sh — oxr-docs-routing 0층 심볼 인덱스 생성기
#
# 무엇: PackageCache 소스에서 "선언 + XML /// summary 첫 줄 + file:line"을 기계 추출한다.
# 왜:   라우팅 병목은 corpus 크기가 아니라 (a) 경로 재발견 (b) grep 패턴 추측 (c) 806줄 통짜 Read.
#       인덱스는 그 셋을 없앤다. 2026-07-27 기준선 측정: 왕복 6회 -> 2회 (baseline 근거는 SKILL.md §0층).
#
# 규칙 (SSOT 위반 아님):
#   - 산문 요약을 사람이 쓰지 않는다. 전부 소스에서 verbatim 기계 추출.
#   - 인덱스는 "지도"다. 근거는 언제나 실제 file:line 을 Read 해서 댄다 (대원칙 1).
#   - 생성물은 커밋 금지 (oxr-sdk private -> Promptscene-harness 는 PUBLIC 레포). .gitignore 로 차단.
#
# 사용: bash build-index.sh [OUTDIR]        (기본 OUTDIR = <repo>/promptscene/.index)
#       ROOT=/c/J_0 bash build-index.sh
set -u

ROOT="${ROOT:-/c/J_0}"
OUT="${1:-$ROOT/promptscene/.index}"
STAMP_ONLY="${STAMP_ONLY:-0}"

# 인덱싱 대상 프로젝트: <디스크 디렉터리>:<인덱스상 이름>
PROJECTS="XumFlow-studio:studio XRCollabDemo:xrcollab"
# 대상 패키지 필터 (FishNet 651파일은 서드파티라 제외 — 필요해지면 여기 추가)
PKG_FILTER='xum|unified'

mkdir -p "$OUT"

# ---------------------------------------------------------------- awk: 선언 추출
read -r -d '' AWK_PROG <<'AWKEOF'
BEGIN { insum=0; sum=""; attr="" }

# 파일이 바뀌면 버퍼 리셋 + 상대경로 계산 (awk 1회 호출로 전 파일 처리 — 스폰 비용 제거)
FNR==1 { insum=0; sum=""; attr=""; REL=substr(FILENAME, length(PCROOT)+2) }

# --- /// <summary> 블록 수집 (의미 텍스트의 실제 소재지) ---
/^[ \t]*\/\/\/[ \t]*<summary>/ { insum=1; sum=""; next }
/^[ \t]*\/\/\/[ \t]*<\/summary>/ { insum=0; next }
insum==1 {
  t=$0
  sub(/^[ \t]*\/\/\/[ \t]*/,"",t)
  gsub(/<see cref="/,"",t); gsub(/<paramref name="/,"",t)
  gsub(/"[ \t]*\/>/,"",t); gsub(/<c>/,"",t); gsub(/<\/c>/,"",t)
  gsub(/<[^>]*>/,"",t)
  gsub(/^[ \t]+/,"",t); gsub(/[ \t]+$/,"",t)
  if (t != "") { if (sum=="") sum=t; else if (length(sum) < 100) sum = sum " " t }
  next
}

# --- /// <param> 등 나머지 doc 줄, 일반 주석: summary 유지 ---
/^[ \t]*\/\// { next }

# --- 속성([ObserversRpc(BufferLast = true)] 같은 것 — 측정에서 답의 일부였다) ---
/^[ \t]*\[/ {
  a=$0; gsub(/^[ \t]+/,"",a); gsub(/[ \t]+$/,"",a)
  attr = (attr=="" ? a : attr " " a)
  next
}

# --- 빈 줄: 문맥 끊김 -> 버퍼 리셋 ---
/^[ \t]*$/ { sum=""; attr=""; next }

{
  d=$0
  gsub(/^[ \t]+/,"",d); gsub(/[ \t]+$/,"",d)
  sub(/[ \t]*\{[ \t]*$/,"",d)
  gsub(/[ \t]+/," ",d)

  isType = (d ~ /^(public|internal|protected|private)( (sealed|abstract|static|partial|readonly|unsafe|new))* (class|struct|interface|enum|record)[ ]/)
  isPub  = (d ~ /^public /)
  hasSum = (sum != "")
  isDecl = (d ~ /[(;=]/ || d ~ /\{ get/ )

  if (isType) {
    name=d
    sub(/^.*(class|struct|interface|enum|record) /,"",name)
    sub(/[ :<].*$/,"",name)
    printf "TYPE\t%s\t%s\t%s:%d\t%s\t%s\n", name, d, REL, FNR, sum, attr
    sum=""; attr=""; next
  }
  # public 이거나, summary 를 가진 선언 (private 도 포함 — SendPropertySnapshot 사례)
  if ((isPub || hasSum) && isDecl) {
    name=d
    sub(/\(.*$/,"",name); sub(/ *[:=].*$/,"",name); sub(/ *\{.*$/,"",name)
    n=split(name,parts," "); name=parts[n]
    printf "MEMBER\t%s\t%s\t%s:%d\t%s\t%s\n", name, d, REL, FNR, sum, attr
    sum=""; attr=""; next
  }
  sum=""; attr=""
}
AWKEOF

# ---------------------------------------------------------------- 프로젝트 루프
for entry in $PROJECTS; do
  disk="${entry%%:*}"; alias="${entry##*:}"
  pc="$ROOT/$disk/Library/PackageCache"
  [ -d "$pc" ] || { echo "skip: $pc 없음"; continue; }
  mkdir -p "$OUT/$alias"

  for pkgdir in $(cd "$pc" && ls -d */ 2>/dev/null | grep -iE "$PKG_FILTER"); do
    pkg="${pkgdir%/}"
    short=$(echo "$pkg" | sed -E 's/^com\.[^.]+\.//; s/@.*$//')
    tf="$OUT/$alias/$short.types.md"
    mf="$OUT/$alias/$short.members.tsv"

    hdr_root="$pc"
    {
      echo "# ${short} — TYPE index (생성물, 커밋 금지)"
      echo "# pkg-dir(해시 스탬프): ${pkg}"
      echo "# abs-root: ${hdr_root}"
      echo "# 재생성 조건: 위 pkg-dir 이 디스크와 다르면 낡음 -> build-index.sh 재실행"
      echo "# 형식: NAME | DECL | relpath:line | summary(소스 /// 에서 기계 추출)"
      echo "#"
    } > "$tf"
    {
      echo "# ${short} — MEMBER index (grep 전용, 통째 Read 금지)"
      echo "# pkg-dir: ${pkg}   abs-root: ${hdr_root}"
      echo "# 형식: NAME <TAB> DECL <TAB> relpath:line <TAB> summary <TAB> attrs"
    } > "$mf"

    [ "$STAMP_ONLY" = "1" ] && continue

    # awk 1회 호출로 패키지 전체 처리 (파일당 스폰 = Windows에서 치명적으로 느림)
    find "$pc/$pkg" -name '*.cs' -not -path '*/Tests/*' -print0 2>/dev/null \
      | xargs -0 awk -v PCROOT="$pc" "$AWK_PROG" \
      | sort -t$'\t' -k1,1 -k2,2 > "$OUT/$alias/.raw.$short"

    # grep -P 는 이 환경에서 로케일 때문에 미지원 -> awk 필드 필터로 대체
    # types 파일은 "통째로 Read" 되는 물건이라 summary 를 잘라 크기를 억제한다 (members 는 안 자름)
    awk -F'\t' '$1=="TYPE" {s=$5; if (length(s)>90) s=substr(s,1,90)"…";
                            printf "%s | %s | %s | %s\n", $2, $3, $4, s}' \
      "$OUT/$alias/.raw.$short" >> "$tf"
    awk -F'\t' '$1=="MEMBER" {print $2"\t"$3"\t"$4"\t"$5"\t"$6}' \
      "$OUT/$alias/.raw.$short" >> "$mf"
    rm -f "$OUT/$alias/.raw.$short"

    printf "%-10s %-18s types=%-5s members=%s\n" "$alias" "$short" \
      "$(( $(wc -l < "$tf") - 6 ))" "$(( $(wc -l < "$mf") - 3 ))"
  done
done

# ---------------------------------------------------------------- T3: 문서 헤딩 인덱스
hd="$OUT/headings.md"
{
  echo "# 문서 헤딩 인덱스 (1층 harness + 2층 패키지 문서)"
  echo "# 형식: relpath:line | heading"
  echo "#"
} > "$hd"
( cd "$ROOT" && grep -rn '^#\{1,3\} ' promptscene/docs/*.md 2>/dev/null \
    | sed -E 's/^([^:]+):([0-9]+):/\1:\2 | /' ) >> "$hd"
for entry in $PROJECTS; do
  disk="${entry%%:*}"; alias="${entry##*:}"
  pc="$ROOT/$disk/Library/PackageCache"
  [ -d "$pc" ] || continue
  # 주의 1: 치환문에 ' | ' 가 들어가므로 sed 구분자로 | 를 쓰면 안 된다 (첫 실행 때 이걸로 깨졌다)
  # 주의 2: PackageCache 전체를 훑으면 FishNet 등 서드파티 문서가 딸려와 10,875줄/1.2MB 로 폭발한다
  #         -> 반드시 PKG_FILTER 에 걸린 패키지 디렉터리 안에서만 찾는다 (2차 실행에서 발견)
  for pkgdir in $(cd "$pc" && ls -d */ 2>/dev/null | grep -iE "$PKG_FILTER"); do
    ( cd "$pc" && find "${pkgdir%/}" -path '*/Documentation*' -name '*.md' 2>/dev/null \
        | while IFS= read -r m; do grep -n '^#\{1,3\} ' "$m" 2>/dev/null \
        | sed -E "s#^([0-9]+):#$alias/$m:\1 | #"; done ) >> "$hd"
  done
done
echo "headings   $(( $(wc -l < "$hd") - 3 )) 줄"

echo
echo "OUT=$OUT  (생성물 — 커밋 금지)"
