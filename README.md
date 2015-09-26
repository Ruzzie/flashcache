# FlashCache
a fixed size high performant in memory cache

[![Build status](https://ci.appveyor.com/api/projects/status/1rt1eq4cmvlrphn7?svg=true)](https://ci.appveyor.com/project/Ruzzie/flashcache) 
[![Coverage Status](https://coveralls.io/repos/Ruzzie/flashcache/badge.svg?branch=master&service=github)](https://coveralls.io/github/Ruzzie/flashcache?branch=master)
[![NuGet](https://img.shields.io/nuget/v/Ruzzie.Cache.FlashCache.svg)](https://www.nuget.org/packages/Ruzzie.Cache.FlashCache)

- Fixed size, everything is done in memory, so control over maximum size is needed to prevent out of memory errors
- Multithreaded, multiple readers and writers are accessing the cache
- Fast as hell
- Newer added values are expected to be read more frequently
- Doesn't need to be LIFO, happy with 'good enough'

## Example usage

### Create a cache

``` csharp
	//creates a cache of approx. 1 mb.
    FlashCache<string, int> flashCache = new FlashCache<string, int>(1);
	
	//When creating a cache for reference types it is advised to pass extra parameters to indicate the estimated size per cache entry.
    // (Since the managed enviroment can give accurate sizes of value types)
 	FlashCache<string, string> cache = new FlashCache<string, string>(4,averageSizeInBytesOfKey:48, averageSizeInBytesOfValue:48);
```
### Get or add an item to the cache
``` csharp
	//If the value is present in the cache the cached value will be returned, 
    //else the valueFactory function will be invoked, that value is returned and stored in cache.
	var valueFromCacheOrValueFactory =  cache.GetOrAdd("MyKey", key => 1);
```

Beware that since fixed sizing of the cache is the primary feature there is no guarantee how long an item will be cached. Since the distribution is based upon the hash, no keys with the same hashcode and different values can be in the cache.
