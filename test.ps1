# cd "RankingListTestNew\bin\Release\net10.0"
$program = ".\RankingListTestNew.exe"
$jsonDir = ".\Test\"
$rankingName = "BucketLinkedListRankingList"

Write-Host $rankingName
$jsonFiles = Get-ChildItem -Path $jsonDir -Filter "*.json" -File

foreach ($file in $jsonFiles) {
    $baseName = $file.BaseName
    Write-Host "Testing: $baseName"
    & $program test $rankingName --base BucketBRTreeRankingList -t $baseName
}
