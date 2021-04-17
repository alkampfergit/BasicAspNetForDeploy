using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using TestWebApp.Core.DataAccess;
using TestWebApp.Core.DbEntities;

namespace TestWebApp.Controllers
{
    public class AdminController : Controller
    {
        public ActionResult Users()
        {
            var dao = new UserDao();
            var users = dao.GetAll();
            return View(users);
        }

        [HttpGet]
        public ActionResult AddUser()
        {
            var user = new User();
            return View(user);
        }

        [HttpPost]
        public ActionResult AddUser(User user)
        {
            var dao = new UserDao();
            dao.UpsertUser(user);
            return RedirectToAction("Users");
        }
    }
}