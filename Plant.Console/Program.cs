﻿using Autofac;
using LinqToDB.DataProvider.Oracle;
using Plant.Model;
using Plant.Services;
using System;

namespace Plant.Console
{
    class Program
    {
		static void Main(string[] args)
		{
			OracleTools.ResolveOracle(typeof(Oracle.ManagedDataAccess.Client.OracleConnection).Assembly);

			var builder = new ContainerBuilder();
			builder.RegisterType<BaseDataProvider>().AsImplementedInterfaces().InstancePerLifetimeScope();
			//data layer
			builder.RegisterType<DataProviderManager>().As<IDataProviderManager>().InstancePerDependency();
			builder.Register(context => context.Resolve<IDataProviderManager>().DataProvider).As<IPlantDataProvider>().InstancePerDependency();

			builder.RegisterType<PlantContext>().AsImplementedInterfaces().InstancePerLifetimeScope();

			builder.RegisterGeneric(typeof(EntityRepository<>)).As(typeof(IRepository<>)).InstancePerLifetimeScope();

			builder.RegisterType<StudentService>().As<IStudentService>().InstancePerLifetimeScope();
			builder.RegisterType<CityService>().As<ICityService>().InstancePerLifetimeScope();
			builder.RegisterType<CountryService>().As<ICountryService>().InstancePerLifetimeScope();
			using (var container = builder.Build())
			{
				var studentService = container.Resolve<IStudentService>();
				var cityService = container.Resolve<ICityService>();
				var countryService = container.Resolve<ICountryService>();
				var students = studentService.GetStudentsList();
				foreach (var item in students)
				{
					System.Console.WriteLine($"{item.Id} {item.FirstName}");
				}
				var student = studentService.GetStudentByID(1);
				System.Console.WriteLine($"{student.Id} {student.FirstName}");

				var cities = cityService.GetCitysList();
				foreach (var item in cities)
				{
					System.Console.WriteLine($"{item.Id} {item.Name}");
				}
				var counties = countryService.GetCountrysList();
				foreach (var item in counties)
				{
					System.Console.WriteLine($"{item.Id} {item.Name}");
				}
			}
		}

    }
}
