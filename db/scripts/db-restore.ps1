$container = "finance_pg"
$db = "finance"
$user = "finance_user"

# 1) Копируем дамп в контейнер
docker cp "db/backup/seed.dump" "${container}:/tmp/seed.dump"

# 2) Чистим схему и восстанавливаем
docker exec -t $container psql -U $user -d $db -c "DROP SCHEMA public CASCADE; CREATE SCHEMA public;"
docker exec -t $container pg_restore -U $user -d $db --clean --if-exists /tmp/seed.dump
docker exec -t $container rm /tmp/seed.dump

Write-Host "Database restored from db/backup/seed.dump"