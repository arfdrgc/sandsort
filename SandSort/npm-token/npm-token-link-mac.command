#!/bin/sh

folder=$(
	cd "$(dirname "$0")"
	pwd -P
)

target_link=~/.upmconfig.toml
source_file=$folder/upmconfig.toml

if [ -L "$target_link" ]; then
	echo "old link found "$target_link" -> "$(readlink -f "$target_link")", replacing"
	rm "$target_link"
fi
if [ -f "$target_link" ]; then
	echo "old token file found, backuping to "$target_link"_back"
	mv -f "$target_link" "$target_link"_back
fi

ln -s "$source_file" "$target_link"
echo "link created "$target_link" -> "$source_file""
