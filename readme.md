what:

snoop on ur local dns queries

how:

dotnet tool install --global dnsnoop

run dnsnoop

```
❯ dnsnoop
     __
 ___/ /  ___   ___  ___  ___  ___    ___
/ _  /  / _ \ (_-< / _ \/ _ \/ _ \  / _ \
\_,_/  /_//_//___//_//_/\___/\___/ / .__/
                                  /_/

1.0.3
using SharpPcap v5.4.0.0
Listening on device Realtek USB NIC for traffic on port 53
Press enter to stop
query   name    server IP       response
AAAA    slack.com       192.168.0.1     NOERROR
AAAA    avatars.githubusercontent.com   192.168.0.1     NOERROR
AAAA    github-cloud.s3.amazonaws.com   192.168.0.1     CNAME: s3-1-w.amazonaws.com
AAAA    github.githubassets.com 192.168.0.1     NOERROR
AAAA    user-images.githubusercontent.com       192.168.0.1     NOERROR
A       lobste.rs       192.168.0.1     A: 67.205.128.5
AAAA    lobste.rs       192.168.0.1     AAAA: 2604:a880:400:d0::1dc9:f001
AAAA    presence.teams.microsoft.com    192.168.0.1     CNAME: presence.services.sfb.trafficmanager.net, CNAME: a-ups-presence0-prod-azsc.japaneast.cloudapp.azure.com
A       clientservices.googleapis.com   192.168.0.1     A: 216.58.196.131
AAAA    clientservices.googleapis.com   192.168.0.1     AAAA: 2404:6800:4006:805::2003
A       wpad.znedw.com  192.168.0.1     NXDOMAIN
AAAA    wpad.znedw.com  192.168.0.1     NXDOMAIN
```

thanks @jvns for the inspo (https://github.com/jvns/dnspeep)