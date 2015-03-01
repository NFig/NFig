# NFig

[![NuGet version](https://badge.fury.io/nu/NFig.svg)](http://badge.fury.io/nu/NFig)
[![Build status](https://ci.appveyor.com/api/projects/status/bkbpuc7xojc2gjtr/branch/master?svg=true)](https://ci.appveyor.com/project/bretcope/nfig/branch/master)

NFig is a settings library which helps you manage both default values, and live overrides. It is built with multiple deployment tiers and data centers in mind.

Currently, the only useful implementation is built on top of Redis (see [NFig.Redis](https://github.com/NFig/NFig.Redis)). However, the core of the library is data-store agnostic, and other implementations could easily be built on top of it.

NFig is a work in progress, and there is no documentation yet, but you can look at the [sample console app](https://github.com/NFig/NFig.Redis/tree/master/SampleApplication) in NFig.Redis to get a feel for how it works.
