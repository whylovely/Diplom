# Сохраняет текущую базу в db/backup/seed.dump
$container = "finance_pg"
$db = "finance"
$user = "finance_user"

New-Item -ItemType Directory -Force -Path "db/backup" | Out-Null

docker exec -t $container pg_dump -U $user -d $db -F c -f /tmp/seed.dump
docker cp "${container}:/tmp/seed.dump" "db/backup/seed.dump"
docker exec -t $container rm /tmp/seed.dump

Write-Host "Backup saved to db/backup/seed.dump"