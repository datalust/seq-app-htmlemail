# Seq Apps [![Build status](https://ci.appveyor.com/api/projects/status/k03t9s0ubtylqixi/branch/main?svg=true)](https://ci.appveyor.com/project/datalust/seq-apps/branch/main) [![Join the chat at https://gitter.im/datalust/seq](https://img.shields.io/gitter/room/datalust/seq.svg)](https://gitter.im/datalust/seq) [![NuGet tag](https://img.shields.io/badge/nuget-seq--app-blue.svg)](https://www.nuget.org/packages?q=seq-app)

Input and output plugins for [Seq](http://datalust.co/seq). You can find installable versions of these by searching for the [seq-app tag on NuGet](https://www.nuget.org/packages?q=seq-app).

**Important note:** The 3.x versions of the Seq App packages require Seq 5.1 or later. For earlier Seq versions, install packages from the Seq Apps 2.x series.

In this repository you'll find:

 * **Email+** - [send log events and alerts as HTML email messages](https://docs.datalust.co/docs/formatting-html-email) when they arrive
 * **File Archive** - write incoming events into a set of rolling text files
 * **First of Type** - raise an event the first time a new event type is seen
 * **Replication** - [forward incoming events](https://docs.datalust.co/docs/event-forwarding) to another Seq server
 * **Thresholds** - raise an event if the frequency of matched events exceeds a threshold
 
Also from Datalust, elsewhere on GitHub:

 * **[Digest Email](https://github.com/datalust/seq-app-digestemail)** - send multiple events as a single HTML email
 * **[Health Check Input](https://github.com/datalust/seq-input-healthcheck)** - periodically GET an HTTP resource and write response metrics to Seq
 * **[GELF Input](https://github.com/datalust/sqelf)** - ingest events GELF format over TCP or UDP
 * **[JSON Archive](https://github.com/datalust/seq-app-jsonarchive)** - write incoming events into a set of rolling files, in JSON format 
 * **[RabbitMQ Input](https://github.com/datalust/seq-input-rabbitmq)** - ingest events from RabbitMQ
 * **[Syslog Input](https://github.com/datalust/squiflog)** - ingest syslog events over UDP
