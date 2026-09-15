using TrailBlaze.Interface.Repository;

namespace TrailBlaze.Repository
{
    public sealed class UserRepository(TrailBlazeContext context) : DatabaseRepository(context), IUserRepository
    {
    }
}
