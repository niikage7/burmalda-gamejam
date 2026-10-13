#!/usr/bin/env sh
# Makes a placeholder picture for a screenshot the docs still need (ImageMagick):
#   Tools/docs/screenshot_placeholder.sh <name> "<what the screenshot must show>"
# Writes Documentation/images/screenshots/<name>.png. To add the real screenshot, save it over this file
# (same name, PNG, landscape ~1280x720): the pages that show it need no edits.
set -e
cd "$(dirname "$0")/../.."
name="$1"
text="$2"
out="Documentation/images/screenshots/$name.png"
font="${DOC_FONT:-/usr/share/fonts/noto/NotoSans-Bold.ttf}"
mkdir -p "$(dirname "$out")"
magick -size 1280x720 xc:'#262c38' \
  -fill none -stroke '#8a94a8' -strokewidth 6 -draw "stroke-dasharray 28 18 roundrectangle 24,24 1255,695 36,36" \
  -stroke none -fill '#ffd23f' -font "$font" -pointsize 64 -gravity north -annotate +0+70 'НУЖЕН СКРИНШОТ' \
  \( -size 1060x360 -background none -fill '#f1f3f7' -font "$font" -pointsize 38 -gravity center caption:"$text" \) \
  -gravity center -geometry +0+40 -composite \
  -fill '#8a94a8' -font "$font" -pointsize 26 -gravity south -annotate +0+56 "Заменить файл: images/screenshots/$name.png" \
  -strip PNG8:"$out"
echo "$out"
