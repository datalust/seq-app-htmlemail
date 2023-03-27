# Seq.App.EmailPlus [![Build status](https://ci.appveyor.com/api/projects/status/k03t9s0ubtylqixi?svg=true)](https://ci.appveyor.com/project/datalust/seq-app-htmlemail) [![NuGet tag](https://img.shields.io/badge/nuget-seq--app-blue.svg)](https://www.nuget.org/packages?q=seq-app)

[Send log events and alerts as HTML email messages](https://docs.datalust.co/docs/formatting-html-email) when they arrive at [Seq](https://datalust.co/seq).

![HTML email app icon](https://raw.githubusercontent.com/datalust/seq-app-htmlemail/dev/asset/seq-app-email-plus.png)

## HTML email templates

The app uses [Handlebars.Net](https://github.com/rexm/Handlebars.Net) for email templating. Check out the [Handlebars Guide](https://handlebarsjs.com/guide/) for information on the Handlebars syntax.

### Event types and properties

All templates can access the following special properties:

 * `$DateTimeFormat` &mdash; the date/time format string defined in the app's settings
 * `$EventType` &mdash; the numeric event type assigned to the event
 * `$EventUri` &mdash; a link to the event in the Seq UI (not valid for alerts)
 * `$Exception` &mdash; the exception associated with the event, if any
 * `$Id` &mdash; the Seq event id
 * `$Instance` &mdash; the Seq server's instance name, if any
 * `$Level` &mdash; the event's level as a string
 * `$LocalTimestamp` &mdash; the timestamp of the event, in the server's time zone (deprecated)
 * `$Message` &mdash; the event's fully-rendered message
 * `$MessageTemplate` &mdash; the event's [message template](https://messagetemplates.org)
 * `$Properties` &mdash; a dictionary containing all first-class event properties (see below)
 * `$ServerUri` &mdash; the Seq server's base URL
 * `$TimeZoneName` &mdash; the time zone name defined in the app's settings
 * `$UtcTimestamp` &mdash; the timestamp of the event

The event's first-class properties do not use prefixed `$` names, so if an event carries a `RequestId` property, this is accessed in the template as `{{RequestId}}`, **not** `{{$RequestId}}`.

Notifications for Alerts can use the properties [listed in the Seq documentation](https://docs.datalust.co/docs/alert-properties).

### Built-in helper functions

The app makes the following helper functions available in templates.

| Name | Description | Example usage |
| --- | --- | --- |
| `datetime` | Format a date time, optionally specifying a time zone | `{{datetime $SliceStart 'o' 'Australia/Brisbane'}}` |
| `if_eq` | Compare two values for equality and optionally execute a specified block | `{{#if_eq $Level 'Error'}} Oops! {{/if_eq}}` |
| `pretty` | Pretty-print a value, converting arrays and objects to JSON | `{{pretty Results}}` |
| `substring` | Compute a substring given start index and optional length | `{{substring Uri 0 5}}` |

Helpers are [defined here](https://github.com/datalust/seq-app-htmlemail/blob/dev/src/Seq.App.EmailPlus/HandlebarsHelpers.cs).

## Other Seq apps

This repository originally included the following apps from Datalust, each now in their own dedicated repositories:

 * **[File Archive](https://github.com/datalust/seq-app-filearchive)** - write incoming events into a set of rolling text files
 * **[First of Type](https://github.com/datalust/seq-app-firstoftype)** - raise an event the first time a new event type is seen
 * **[Replication](https://github.com/datalust/seq-app-replication)** - [forward incoming events](https://docs.datalust.co/docs/event-forwarding) to another Seq server
 * **[Thresholds](https://github.com/datalust/seq-app-thresholds)** - raise an event if the frequency of matched events exceeds a threshold
 * **[Digest Email](https://github.com/datalust/seq-app-digestemail)** - send multiple events as a single HTML email
 * **[Health Check Input](https://github.com/datalust/seq-input-healthcheck)** - periodically GET an HTTP resource and write response metrics to Seq
 * **[GELF Input](https://github.com/datalust/sqelf)** - ingest events GELF format over TCP or UDP
 * **[JSON Archive](https://github.com/datalust/seq-app-jsonarchive)** - write incoming events into a set of rolling files, in JSON format 
 * **[RabbitMQ Input](https://github.com/datalust/seq-input-rabbitmq)** - ingest events from RabbitMQ
 * **[Syslog Input](https://github.com/datalust/squiflog)** - ingest syslog events over UDP
