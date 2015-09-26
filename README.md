# FlashCache
a fixed size high performant in memory cache

[![Build status](https://ci.appveyor.com/api/projects/status/1rt1eq4cmvlrphn7?svg=true)](https://ci.appveyor.com/project/Ruzzie/flashcache) 
[![Coverage Status](https://coveralls.io/repos/Ruzzie/flashcache/badge.svg?branch=master&service=github)](https://coveralls.io/github/Ruzzie/flashcache?branch=master)
[![NuGet](https://img.shields.io/nuget/v/Ruzzie.Cache.FlashCache.svg)](https://www.nuget.org/packages/Ruzzie.Cache.FlashCache)

https://img.shields.io/nuget/dt/Ruzzie.Cache.FlashCache.svg

- Fixed size, all is done in memory, so control over maximum size is needed to prevent out of memory errors
- Multithreaded, multiple readers and writers are accessing the cache
- Fast as hell
- Newer added values are expected to be read more frequently
- Doesn't need to be LIFO, happy with 'good enough'
