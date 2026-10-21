#!/bin/sh
set -eu

: "${OBJECT_STORAGE_BUCKET:?OBJECT_STORAGE_BUCKET is required}"
: "${AWS_ACCESS_KEY_ID:?AWS_ACCESS_KEY_ID is required}"
: "${AWS_SECRET_ACCESS_KEY:?AWS_SECRET_ACCESS_KEY is required}"
endpoint="http://seaweedfs:8333"
case "$OBJECT_STORAGE_BUCKET" in
	*[!a-z0-9.-]*|'') printf '%s\n' 'Invalid bucket name' >&2; exit 1 ;;
esac

# Listing must succeed before creating anything; connection/authentication failures are fatal.
bucket=$(aws --endpoint-url "$endpoint" s3api list-buckets \
	--query "Buckets[?Name=='${OBJECT_STORAGE_BUCKET}'].Name | [0]" --output text)
if [ "$bucket" = "None" ]; then
	aws --endpoint-url "$endpoint" s3api create-bucket --bucket "$OBJECT_STORAGE_BUCKET" >/dev/null
fi
aws --endpoint-url "$endpoint" s3api head-bucket --bucket "$OBJECT_STORAGE_BUCKET"
