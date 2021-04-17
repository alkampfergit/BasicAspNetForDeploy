using NUnit.Framework;
using System;
using System.Linq;
using TestWebApp.Core.DataAccess;
using TestWebApp.Core.DbEntities;

namespace TestWebApp.Tests.Core.Sql
{
    [TestFixture]
    public class VeryBasicDataAccessTests
    {
        [Test]
        public void Verify_insert_user_assign_id()
        {
            var uid = Guid.NewGuid().ToString();
            var user = new User()
            {
                Email = uid + "@test.com",
                UserName = uid,
                Name = "TEST USEr" + uid
            };
            var sut = new UserDao();
            sut.UpsertUser(user);
            Assert.That(user.Id, Is.GreaterThan(0));
        }

        [Test]
        public void Verify_can_read_users()
        {
            var uid = Guid.NewGuid().ToString();
            var user = new User()
            {
                Email = uid + "@test.com",
                UserName = uid,
                Name = "TEST USEr" + uid
            };
            var sut = new UserDao();
            sut.UpsertUser(user);
            var users = sut.GetAll();
            Assert.That(users.Any(u => u.UserName == uid));
        }
    }
}
