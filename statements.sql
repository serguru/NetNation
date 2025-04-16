if (object_id('chargeable') is not null)
	drop table chargeable;
go

create table chargeable
(
id int not null identity primary key,
partnerID int,
product varchar(max),
partnerPurchasedPlanID varchar(max),
[plan] varchar(max),
usage int
)


if (object_id('domains') is not null)
	drop table domains;
go
create table domains
(
id int not null identity primary key,
partnerPurchasedPlanID varchar(max),
domain varchar(max)
)
go


