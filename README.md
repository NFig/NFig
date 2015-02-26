# NFig

NFig is a settings library which helps you manage both default values, and live overrides. It is built with multiple deployment tiers and data centers in mind.

Currently, the only implementation is built on top of Redis. However, the core of the library is data-store agnostic, and other implementations could easily be built on top of it.

NFig is a work in progress, and there is no documentation yet, but you can look at the [sample console app](https://github.com/NFig/NFig/tree/master/SampleApplication) to get a feel for how it works. The NFig.Redis store is available [on nuget.org](https://www.nuget.org/packages/NFig.Redis/) as a pre-release version. NFig.Redis will eventually become its own repository, and the core NFig library will be published to nuget as well.
