export user=$1
export apitoken=$2
export pkg=$3
curl -vX PUT -u "$user:$apitoken" -F package=@$pkg https://nuget.pkg.github.com/$user/