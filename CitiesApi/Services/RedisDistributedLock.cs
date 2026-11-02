
using StackExchange.Redis;

namespace CitiesApi.Services
{
    public class RedisDistributedLock
    {
        private readonly IDatabase _database;

        public RedisDistributedLock(IConnectionMultiplexer redis)
        {
            _database = redis.GetDatabase();
        }

        public async Task<bool> AcquireLockAsync(
            string lockKey,
            string lockValue,
            TimeSpan expiry)
        {
            return await _database.StringSetAsync(
                lockKey,
                lockValue,
                expiry,
                When.NotExists
            );
        }

        public async Task ReleaseLockAsync(
            string lockKey,
            string lockValue)
        {
            const string script = @"
                if redis.call('GET', KEYS[1]) == ARGV[1] then
                    return redis.call('DEL', KEYS[1])
                else
                    return 0
                end";

            await _database.ScriptEvaluateAsync(
                script,
                new RedisKey[] { lockKey },
                new RedisValue[] { lockValue }
            );
        }
    }
}
