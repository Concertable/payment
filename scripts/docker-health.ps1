<#
docker-health.ps1 — robust Docker pre-flight for the E2E suites.

WHY THIS EXISTS (do not weaken it):
`docker ps` answering is NOT proof Docker is healthy. A half-started / paused /
half-crashed Docker Desktop engine can keep an ALREADY-RUNNING container's port
forwarding alive (and keep answering `docker ps`, even `docker run hello-world`)
while host->container port forwarding for BRAND-NEW containers is dead. The E2E
suites don't notice until much later, at SQL fixture startup, where every
connection is accepted then reset ("pre-login handshake" / Win32 10053) and the
whole suite dies after a few minutes with ZERO scenarios executed.

A bare TCP connect is NOT enough either: the host-side `docker-proxy` completes
the TCP handshake locally even when forwarding of real bytes into the VM/container
is dead -- which is exactly SQL's "connection established, then pre-login handshake
aborted" mode. The only check that catches the bad state is the one the suites
actually depend on: create a FRESH container that publishes a port, then do a real
host->container DATA round-trip (an HTTP GET that returns bytes from inside the
container), and require it to be STABLE (the half-started engine flaps). That is
what this script does, then tears the probe down.

Exit 0 = healthy (safe to run E2E). Exit 1 = NOT healthy (do NOT run E2E; the
caller must stop and tell the user to fix Docker first).
#>

$probeName = 'concertable-dockerprobe'
$image     = 'nginx:alpine'   # tiny (~10MB), listens on :80 immediately

function Write-Fail([string]$msg) {
    Write-Host ""
    Write-Host "DOCKER UNHEALTHY -- E2E will fail at fixture startup. NOT running the suite." -ForegroundColor Red
    Write-Host "  $msg" -ForegroundColor Red
    Write-Host "  Fix: open Docker Desktop, wait until it shows 'Running' (restart it if it's" -ForegroundColor Yellow
    Write-Host "  stuck/paused), then re-run. Do not debug application code for this failure." -ForegroundColor Yellow
    Write-Host ""
    docker rm -f $probeName 2>$null | Out-Null
}

# A REAL data round-trip: bytes must travel host->proxy->VM->container and back.
# A bare TCP connect would pass at the proxy even when forwarding is dead.
function Get-HttpOk([int]$p) {
    try {
        $r = Invoke-WebRequest -Uri "http://127.0.0.1:$p/" -TimeoutSec 4 -UseBasicParsing -ErrorAction Stop
        return $r.StatusCode -eq 200
    } catch {
        return $false
    }
}

# 1. Daemon reachable at all.
$null = docker ps
if ($LASTEXITCODE -ne 0) {
    Write-Fail "'docker ps' failed -- the Docker daemon is unreachable (Desktop not started)."
    exit 1
}

# 2. Engine can produce/inspect images (pull the tiny probe image if absent).
docker image inspect $image *> $null
if ($LASTEXITCODE -ne 0) {
    Write-Host "Pulling probe image $image (one-time)..." -ForegroundColor Gray
    docker pull $image *> $null
    if ($LASTEXITCODE -ne 0) {
        Write-Fail "could not pull $image -- engine cannot fetch new images."
        exit 1
    }
}

# 3. THE check that matters: fresh container + real host->container round-trip.
docker rm -f $probeName 2>$null | Out-Null

# Grab a guaranteed-free loopback port via an ephemeral .NET listener.
$listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
$listener.Start()
$port = $listener.LocalEndpoint.Port
$listener.Stop()

docker run -d --rm --name $probeName -p "127.0.0.1:${port}:80" $image *> $null
if ($LASTEXITCODE -ne 0) {
    Write-Fail "could not start a fresh container -- the engine cannot run new containers."
    exit 1
}

try {
    # Wait up to ~20s for the first real HTTP response (container warm-up).
    $first = $false
    foreach ($i in 1..20) {
        if (Get-HttpOk $port) { $first = $true; break }
        Start-Sleep -Seconds 1
    }

    # Stability: require 4 consecutive data round-trips -- the half-started engine
    # flaps (forwarding works for a moment, then dies), so one success isn't proof.
    $stable = $false
    if ($first) {
        $stable = $true
        foreach ($i in 1..4) {
            if (-not (Get-HttpOk $port)) { $stable = $false; break }
            Start-Sleep -Milliseconds 400
        }
    }
} finally {
    docker rm -f $probeName 2>$null | Out-Null
}

if (-not $first) {
    Write-Fail "a fresh container started but no real data round-trips back from it -- host->container forwarding is DEAD (TCP may connect at the proxy, but bytes don't reach the container; the SQL pre-login-handshake signature). New SQL containers would accept-then-reset every connection and the suite would die at fixture startup."
    exit 1
}
if (-not $stable) {
    Write-Fail "host->container forwarding is FLAPPING -- a data round-trip worked once then failed. The engine is half-started; wait for Docker Desktop to fully settle on 'Running' and retry."
    exit 1
}

Write-Host "Docker healthy: fresh-container host->container DATA round-trip stable (port $port)." -ForegroundColor Green
exit 0
