#!/bin/bash
 
full_path=$(realpath $0)
dir_path=$(dirname $full_path)
samples=$(dirname $dir_path )

dotnet run --project $samples/SampleServer/SampleServer.csproj --Urls "http://localhost:10000;http://localhost:10001;http://localhost:10002;http://localhost:10003;http://localhost:10004;http://localhost:10005;http://localhost:10006;http://localhost:10007;http://localhost:10008;http://localhost:10009"