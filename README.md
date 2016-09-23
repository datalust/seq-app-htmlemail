# Seq Apps [![Build status](https://ci.appveyor.com/api/projects/status/k03t9s0ubtylqixi/branch/master?svg=true)](https://ci.appveyor.com/project/seqlogs/seq-apps/branch/master) [![Join the chat at https://gitter.im/datalust/seq](https://img.shields.io/gitter/room/datalust/seq.svg)](https://gitter.im/datalust/seq) [![NuGet tag](https://img.shields.io/badge/nuget-seq--app-blue.svg)](https://www.nuget.org/packages?q=seq-app)

Apps for the [Seq](http://getseq.net) event server. You can find installable versions of these by searching for the [seq-app tag on NuGet](https://www.nuget.org/packages?q=seq-app).

**Important note:** The 2.x versions of the Seq App packages require Seq 3.3 or later. For earlier Seq versions, install packages from the Seq Apps 1.x series.

Currently in this repository you'll find:

 * **Email+** - [send log events as HTML email messages](http://docs.getseq.net/v3/docs/formatting-html-email) when they arrive (replaces the obsolete _Email_ app package)
 * **File Archive** - copy incoming log events into a set of rolling text files
 * **First of Type** - raise an event the first time a new event type is seen
 * **Replication** - forward incoming events to another Seq server
 * **Thresholds** - raise an event if the frequency of matched events exceeds a threshold

