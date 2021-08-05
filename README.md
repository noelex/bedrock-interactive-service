# Bedrock Interactive Service

Bedrock Interactive Service is a Windows Service host for interactive console applications. It's primarily written for Minecraft [Bedrock Dedicated Server](https://www.minecraft.net/download/server/bedrock/), but also works for any other interactive console application.

# Motivation

Bedrock Dedicated Server, as it's name suggests, is a server application, which is supposed to run as a service. But unfortunately, it's actually an interative console application running on desktop, thus lacks of some nice features offered by Windows Service, such as automatic startup, crash recovery and most importantly, I can't stop it by accident.

So I wrote this small utility to host Bedrock Dedicated Server inside Windows Service, while still allowing me to connect to the server over TCP/IP to execute those nice server commands.

# Installing

Download a copy of realse archive from [release page](https://github.com/noelex/bedrock-interactive-service/releases), and extract it to anywhere you like.

Please note that the server application `iss` operates Windows Service, thus the release page provides Windows binaries only. You can run the client application `isc` on Windows, Linux and MacOS.

# Usage

Bedrock Interactive Service comes with two console applications, `iss` and `isc`.
`iss` is the server application to host interactive console applications, and `isc` is a client application used to connect to and interact with hosted applications.

## Running on desktop

Before hosting your application as Windows Service, you should test it on desktop first. You can execute `iss` directly in a command prompt:
```
iss --stop-command stop --stop-message "Quit correctly" C:\Bedrock\bedrock_server.exe
```
The above command invokes `bedrock_server.exe` when started, and opens TCP port 21331 on localhost waiting for `isc` to connect. When stopping `iss`,
it will first send "stop" to `bedrock_server.exe` and wait for it to print "Quit correctly", allowing `bedrock_server.exe` to perform clean shutdown.

## Connecting to hosted application

To connect to a hosted application, simply execute `isc` and it will try to connect Bedrock Interactive Service running on localhost.

In case you want to connect services hosted on another computer or another port, you can write `isc -p 21332 192.168.1.10`.

Please note that a single instance of Bedrock Interative Service allows only one client to connect in the same time.

After successfully connecting to the server, you can type commands and see output just like using the hosted application directly.

## Running as service

To run as Windows Service, simply use Windows `sc create` command create a service with the `iss` command you've tested previously:
```
sc create "Bedrock Service" start= auto binpath= "C:\Bedrock\iss.exe --stop-command stop --stop-message \"Quit correctly\" C:\Bedrock\bedrock_server.exe"
```
Please note that double quotes inside `binpath` parameters are escaped.

There're some other parameters you can tune for `sc create` command, please consult its documentation [here](https://docs.microsoft.com/windows-server/administration/windows-commands/sc-create).

After creating the service, you can use `sc start` command, `services.msc` management console or task manager to start your service.

## Inspecting application output

While running as service, you'll not be able to see it's output on the console.
To inspect the ouput you can either connect to the service with `isc`, or use a debug trace viewer ([DebugView](https://docs.microsoft.com/en-us/sysinternals/downloads/debugview) for example).

## Configuring and deleting the service

If you want to change startup parameters for `iss`, can use `sc config` command to change service configurations.

To delete a service, exeute `sc delete "Bedrock Service"`.

## Hosting vellum

[vellum](https://github.com/clvrkio/vellum) is a great backup tool for Bedrock Dedicated Server which supports automated hot backup and map rendering.

You can use `iss` to host vellum instead of hosting Bedrock Dedicated Server directly, to benifit from vellum's backup feature.

Before you create the service, run `vellum` to create a `configuration.json` file,
adjust the configurations according to your needs.

Now execute the following command:
```
sc create "Vellum Managed Bedrock Service" start= auto binpath= "C:\Bedrock\iss.exe --stop-command stop --stop-message \"vellum quit correctly\" C:\Bedrock\vellum.exe"
```
and start the service:
```
sc start "Vellum Managed Bedrock Service"
```

Connect to the service with `isc` utility, now you can type vellum and Bedrock server commands as normal.

# Security condierations

Bedrock Interactive Server does not implement any authentication mechanism. It's highly recommended to configure `iss` to listen on trusted network only. If you want to connect servers on the internet, you should use `ssh` or similar tools to create a secure tunnel, and forward `iss` port via the secure tunnel.

`iss` can also host `cmd.exe` and other command prompts. Before doing so, please
make sure that you understand the risk of exposing your command prompt to the public
network. Always use a secure tunnel, and make sure that you have configured the privilege of the service correctly.